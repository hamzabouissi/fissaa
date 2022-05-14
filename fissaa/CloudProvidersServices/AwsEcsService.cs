using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.ECS;
using Amazon.ECS.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;
using ResourceNotFoundException = Amazon.CloudWatchLogs.Model.ResourceNotFoundException;
using Tag = Amazon.CloudFormation.Model.Tag;
using Task = System.Threading.Tasks.Task;


namespace fissaa.CloudProvidersServices;

public enum ContainerTemplate
{
    App,
    Ghost
}

public class AwsEcsService
{

    private readonly string _domainName;
    private readonly string _baseDomain;
    public readonly string EscapedBaseDomain;
    private readonly string _escapedDomain;
    public string AlbStackName => $"{EscapedBaseDomain}-alb-stack";
    public string ServiceStackName => $"{_escapedDomain}-Service-Stack";
    public string AlarmStackName => $"{_escapedDomain}-Alarm-Stack";
    public string ServiceName => $"{_escapedDomain}-Service";
    public string RepoName => _escapedDomain;
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    public readonly AmazonCloudFormationClient ClientCformation;
    private readonly AwsDomainService _domainServices;
    private readonly AwsUtilFunctions _awsUtilFunctions;
    private readonly AmazonCloudWatchLogsClient _cloudWatchLogsClient;
    private readonly AmazonECRClient _ecrClient;
    private readonly AmazonECSClient _ecsClient;

    public AwsEcsService(string awsSecretKey,string awsAccessKey, string domainName)
    {
        
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        _cloudWatchLogsClient = new AmazonCloudWatchLogsClient(auth,region:Region);
        _domainServices = new AwsDomainService(awsSecretKey, awsAccessKey);
        _ecrClient = new AmazonECRClient(auth, region: Region);
        _ecsClient = new AmazonECSClient(auth, region: Region);
        
        _awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey);
        
        _domainName = domainName;
        _baseDomain = string.Join(".",_domainName.Split(".")[^2..]);
        EscapedBaseDomain = string.Join("-",_domainName.Split(".")[^2..]);
        _escapedDomain = _domainName.Replace(".", "-");
    }
    
    
   
    public async Task Destroy()
    {
        await _awsUtilFunctions.DeleteStack(ServiceStackName);
        await _awsUtilFunctions.DeleteEcrImages(RepoName);
    }

    public async Task<Result> Create(ContainerTemplate template, string dockerfile,
        bool addMonitor,string envFile)
    {
        var cloudFile = await _awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/service.yml");
        var baseDomain = string.Join(".", _domainName.Split(".")[^2..]);
        var hostedZoneId = await _domainServices.GetHostedZoneId(baseDomain);
        var dockerImage = string.Empty; 
        if (template == ContainerTemplate.App)
            dockerImage = await ImageDeployment(dockerfile);
        var priorityNumber = await _awsUtilFunctions.GetListenerRuleNextPriorityNumber(AlbStackName);
        var taskDefinition = await CreateTaskDefinition(template,AppEnvironment.Dev, dockerImage, addMonitor,envFile);
        try
        {
            var parameters = CreateEcsCloudformationParameters(template,taskDefinition, priorityNumber, hostedZoneId);
            return await Deploy(parameters, cloudFile);
        }
        catch (AlreadyExistsException)
        {
            return await UpdateEcsImage(taskDefinition);
        }

    }

    private async Task<Result> Deploy(List<Parameter> parameters, string cloudFile)
    {
       
        Console.WriteLine("Start Deploying");
        await ClientCformation.CreateStackAsync(new CreateStackRequest
        {
            OnFailure = OnFailure.DELETE,
            Parameters = parameters,
            StackName = ServiceStackName,
            TemplateBody = cloudFile,
            TimeoutInMinutes = 30,
            Tags = new List<Tag>()
            {
                new()
                {
                    Key = "app-domain",
                    Value = _baseDomain
                }
            }
        });
        Console.WriteLine("Waiting Stack");
        await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(ServiceStackName);
        var status = await _awsUtilFunctions.GetStackStatus(ServiceStackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status)
            ? Result.Success(status)
            : Result.Failure("creating app failed");
          
    }

    private async Task<string> CreateTaskDefinition(ContainerTemplate template,AppEnvironment appEnvironment=AppEnvironment.Dev, string containerImage = "",
        bool addMonitring = false, string envFile="")
    {
        var stack = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = AlbStackName
        });
        var executionRoleArn = stack.Stacks.First().Outputs.Single(p => p.OutputKey == "ECSTaskExecutionRole").OutputValue;
        var containerDefinitions = new List<ContainerDefinition>();
        var taskCpu = appEnvironment == AppEnvironment.Dev ? 256 :512;
        var taskMemory =  appEnvironment == AppEnvironment.Dev ? 512 :2024;
        var containerPort = 80;
        EnvironmentFile? environment = null;

        switch (template)
        {
            case ContainerTemplate.App:
                if (string.IsNullOrEmpty(containerImage))
                    throw new ArgumentOutOfRangeException(nameof(containerImage), template, "containerImage cannot be null when you deploy your own app");
                break;
            case ContainerTemplate.Ghost:
                if (string.IsNullOrEmpty(envFile))
                    throw new ArgumentOutOfRangeException(nameof(containerImage), template, "env_file cannot be null when you deploy your ghost app");
                containerImage = "docker.io/spectreb/ghost-with-s3";
                containerPort = 2368;
                environment = new EnvironmentFile()
                {
                    Type = EnvironmentFileType.S3,
                    Value = $"arn:aws:s3:::{envFile}"
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(template), template, $"{template} isn't a valid option");
        }

        containerDefinitions.Add(new()
        {
            Cpu = taskCpu,
            EnvironmentFiles = environment is null ? null : new List<EnvironmentFile>()
            {
                environment
            },
            Essential = true,
            Image = containerImage,
            LogConfiguration = new LogConfiguration
            {
                LogDriver = LogDriver.Awslogs,
                Options = new Dictionary<string, string>()
                {
                    {"awslogs-group",ServiceName},
                    {"awslogs-region",Region.SystemName},
                    {"awslogs-stream-prefix",ServiceName}
                },
            },
            Memory = taskMemory,
            Name = ServiceName,
            PortMappings = new List<PortMapping>()
            {
                new()
                {
                    ContainerPort = containerPort,
                }
            },
            
        });
        if (addMonitring)
        {
            containerDefinitions.Add(new ContainerDefinition()
            {
                Name = "xray-daemon",
                Image = "amazon/aws-xray-daemon",
                Cpu = 32,
                MemoryReservation = 256,
                PortMappings = new List<PortMapping>()
                {
                    new PortMapping()
                    {
                        ContainerPort = 200,
                        Protocol = TransportProtocol.Udp
                    }
                },
                
            });
        }
        var taskDefinitionResponse = await _ecsClient.RegisterTaskDefinitionAsync(new RegisterTaskDefinitionRequest
        {
            ContainerDefinitions = containerDefinitions,
            Cpu = "512",
            ExecutionRoleArn = executionRoleArn,
            TaskRoleArn = executionRoleArn,
            Family = ServiceName,
            Memory = "2048",
            NetworkMode = NetworkMode.Awsvpc,
            RequiresCompatibilities = new List<string>()
            {
                "FARGATE"
            } ,
            Tags = new List<Amazon.ECS.Model.Tag>()
            {
                new()
                {
                    Key = "app-domain",
                    Value = _baseDomain
                }
            }
           
        });
        return taskDefinitionResponse.TaskDefinition.TaskDefinitionArn;
    }


    private List<Parameter> CreateEcsCloudformationParameters(ContainerTemplate containerTemplate,
        string taskDefinition, int priorityNumber, string hostedZoneId, string containerPort = "80")
    {
        if (containerTemplate == ContainerTemplate.Ghost)
            containerPort = "2368";
        var parameters = new List<Parameter>()
        {
            new()
            {
                ParameterKey = "TaskDefinitionArn",
                ParameterValue = taskDefinition,
            
            },
            new()
            {
                ParameterKey = "AlbStackName",
                ParameterValue = AlbStackName,
            },
            new()
            {
                ParameterKey = "ServiceName",
                ParameterValue = ServiceName,
            },
            new()
            {
                ParameterKey = "HealthCheckIntervalSeconds",
                ParameterValue = "90"
            },
            new()
            {
                ParameterKey = "Priority",
                ParameterValue = priorityNumber.ToString(),
            },
            new()
            {
                ParameterKey = "Domain",
                ParameterValue = _domainName,
            },
            new()
            {
                ParameterKey = "HostedZoneId",
                ParameterValue = hostedZoneId,
            },
            new()
            {
                ParameterKey = "ContainerPort",
                ParameterValue = containerPort
            }
        };
        return parameters;
    }

    public async Task ListLogs(string startDate,int hour)
    {
        try
        {
            var logStreamsResponse = await _cloudWatchLogsClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {

                LogGroupName = ServiceName,
                LogStreamNamePrefix = ServiceName,

            });
            var logStreamName = logStreamsResponse.LogStreams.Last().LogStreamName;
            DateTime.TryParse(startDate, out var parsedStartDate);
            Console.WriteLine($"Logs from {parsedStartDate} => {parsedStartDate.AddHours(hour)} from Log Group {ServiceName}");
            var logsResponse = await _cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
            {
                // StartTime =  parsedStartDate,
                // EndTime = parsedStartDate.AddHours(hour),
                Limit = 500,
                LogGroupName = ServiceName,
                LogStreamName = logStreamName,
                StartFromHead = false,
            });
            foreach (var logEvent in logsResponse.Events)
                Console.WriteLine(logEvent.Message);

        }
        catch (ResourceNotFoundException)
        {
            throw new ResourceNotFoundException("Domain name not found");
        }

       
       
    }
    
    private async Task<Result> UpdateEcsImage(string task)
    {
        Console.WriteLine($"updating service stack {ServiceStackName}");
        var template = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = ServiceStackName
        });
        var parameters = template.Stacks.First().Parameters;
        parameters.Single(p => p.ParameterKey == "TaskDefinitionArn").ParameterValue = task;
       
        await ClientCformation.UpdateStackAsync(new UpdateStackRequest
        {
            Parameters = parameters,
            StackName = ServiceStackName,
            UsePreviousTemplate = true
        });
      
        Console.WriteLine("Waiting Stack");
        await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(ServiceStackName);
        var status = await _awsUtilFunctions.GetStackStatus(ServiceStackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure("updating app failed") ;
    }

    private async Task<string> ImageDeployment(string dockerfile)
    {
        await _awsUtilFunctions.CreateReposityIfNotExist(RepoName);
        Console.WriteLine("Build Image");
        var (image, registry) = await _awsUtilFunctions.BuildImage(dockerfile, RepoName);
        var password = await _awsUtilFunctions.DecodeRegistryLoginTokenToPassword();
        await AwsUtilFunctions.LoginToRegistry(password, registry);
        Console.WriteLine("Deploy Image");
        await _awsUtilFunctions.DeployImageToEcr(image);
        Console.WriteLine("Deploy Image Ended");
        return image;
    }

    public async Task<Result> RollBackApply(bool? latest, string imageVersion)
    {
        var accountId = await _awsUtilFunctions.GetAccountId();
        var image = $"{accountId}.dkr.ecr.{Region.SystemName}.amazonaws.com/{RepoName}:{imageVersion}";
        var listTaskDefinitionsResponse = await _ecsClient.ListTaskDefinitionsAsync(new ListTaskDefinitionsRequest
        {
            FamilyPrefix = ServiceName,
            Sort = "DESC",
        });
        foreach (var taskDefinitionArn in listTaskDefinitionsResponse.TaskDefinitionArns)
        {
            var definitionResponse = await _ecsClient.DescribeTaskDefinitionAsync(new DescribeTaskDefinitionRequest
            {
                TaskDefinition = taskDefinitionArn
            });
            var imageExist = definitionResponse.TaskDefinition.ContainerDefinitions.Exists(p => p.Image == image);
            if (imageExist)
            {
                var updateResult = await UpdateEcsImage(taskDefinitionArn);
                return updateResult;
            }
        }
        return Result.Failure($"{imageVersion} hasn't been deployed before");

    }
    
    public async Task RollBackList()
    {
        var images = await _ecrClient.DescribeImagesAsync(new DescribeImagesRequest
        {
            RepositoryName = RepoName
        });
        foreach (var image in images.ImageDetails.OrderByDescending(p=>p.ImagePushedAt))
        {
            Console.WriteLine($"DateTime: {image.ImagePushedAt}, ImageTag: {string.Join(' ',image.ImageTags)} ");
        }
    }

    public async Task<Result> CreateAlarm(string email)
    {
        var cloudFile = await _awsUtilFunctions.ExtractTextFromRemoteFile(
            "https://fissaa-cli.s3.amazonaws.com/test/ElbAlarm.yml");
        var albStacksResponse = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = AlbStackName
        });
        var serviceStacksResponse = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = ServiceStackName
        });

        var targetGroupArn = serviceStacksResponse.Stacks.First().Outputs.Single(x => x.OutputKey == "TargetGroup")
            .OutputValue.Split("/",2).Last();
        // 
        var loadBalancerArn = albStacksResponse.Stacks.First().Outputs.Single(x => x.OutputKey == "LoadBalancer").OutputValue
           .Split("/",2).Last();

        
        var parameters = new List<Parameter>()
        {
            new Parameter
            {
                ParameterKey = "Email",
                ParameterValue = email,
            },
            new Parameter
            {
                ParameterKey = "ServiceName",
                ParameterValue = ServiceName,
            },
            new Parameter
            {
                ParameterKey = "LoadBalancerArn",
                ParameterValue = loadBalancerArn,
            },
            new Parameter
            {
                ParameterKey = "TargetGroupArn",
                ParameterValue = $"targetgroup/{targetGroupArn}",
            },
        };
        await ClientCformation.CreateStackAsync(new CreateStackRequest
        {
            OnFailure = OnFailure.DO_NOTHING,
            Parameters = parameters,
            StackName = AlarmStackName,
            TemplateBody = cloudFile,
            TimeoutInMinutes = 30,
            Tags = new List<Tag>()
            {
                new()
                {
                    Key = "app-domain",
                    Value = _baseDomain
                }
        
            }
        });
        Console.WriteLine("Waiting Stack");
        await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(AlarmStackName);
        var status = await _awsUtilFunctions.GetStackStatus(AlarmStackName);
        return _awsUtilFunctions.StackStatusIsSuccessfull(status)
            ? Result.Success(status)
            : Result.Failure("creating alarm failed");
    }

    // public async Task Exec()
    // {
    //     var executeCommandResponse = await ecsClient.ExecuteCommandAsync(new ExecuteCommandRequest
    //     {
    //         Cluster = null,
    //         Command = null,
    //         Container = null,
    //         Interactive = false,
    //         Task = null
    //     });
    // }
}

public enum AppEnvironment
{
    Dev,
    Prod
}
