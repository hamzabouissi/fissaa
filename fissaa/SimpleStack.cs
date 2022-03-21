
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.ECS.Model;
using Amazon.ECS;
using Amazon.IdentityManagement;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CliWrap;
using CliWrap.Buffered;
using Flurl.Http;
using ResourceType = Amazon.EC2.ResourceType;
using Tag = Amazon.EC2.Model.Tag;
using Task = System.Threading.Tasks.Task;
using TransportProtocol = Amazon.ECS.TransportProtocol;


namespace fissaa;

public class SimpleStack
{
    private AmazonEC2Client ClientEc2 { get; }
    private AmazonECSClient ClientEcs { get;}
    private AmazonECRClient ClientEcr { get; }
    public AmazonSecurityTokenServiceClient StsClient { get; set; }
    public AmazonCloudFormationClient ClientCformation { get; set; }


    private AmazonIdentityManagementServiceClient ClientIam { get; }

    private string ProjectName { get; set; }

    public string ServiceStackName => $"{ProjectName}-Service-Stack";
    public string MainStackName => $"{ProjectName}-Main-Stack";
    public string ServiceName => $"{ProjectName}-Service";
    public string ClusterName => $"{ProjectName}-Cluster";
    public string RepoName => ProjectName;
    public RegionEndpoint Region { get; set; } = RegionEndpoint.USEast1;
    public SimpleStack(string awsSecretKey,string awsAccessKey, string projectName)
    {

        ProjectName = projectName;
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        
        ClientEc2 = new AmazonEC2Client(credentials:auth,Region);
        ClientEcs = new AmazonECSClient(credentials:auth,Region);
        ClientEcr = new AmazonECRClient(credentials:auth,Region);
        ClientIam = new AmazonIdentityManagementServiceClient(auth,Region);
        StsClient = new AmazonSecurityTokenServiceClient(auth,Region);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);



    }

    #region UtilFunction

    public async Task<string> GetAccountId()
    {
        var getCallerIdentityResponse = await StsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
        var accountId = getCallerIdentityResponse.Arn.Split(":")[4];
        return accountId;
    }


    public string GetRegistry(string accountId,string region="us-east-1")=>$"{accountId}.dkr.ecr.{region}.amazonaws.com";
    private List<TagSpecification> CreateTag(ResourceType resourceType)
    {
        return new List<TagSpecification>()
        {
            new TagSpecification
            {
                ResourceType = resourceType,
                Tags = new()
                {
                    
                    new Tag
                    {
                        Key = "Project",
                        Value = ProjectName
                    }
                }
            }
        };
    }
    private async Task<string> ExtractTextFromRemoteFile(string url)
    {
        var text = await url.GetStringAsync();
        return text;
    }

    #endregion




    private async Task<string> GetTaskId(string taskDefinitionArn)
   {
       var response = await ClientEcs.ListTasksAsync(new ListTasksRequest
       {
           Cluster = ClusterName,
           Family = null,
           MaxResults = 1,
           ServiceName = ServiceName,
       });

       await ClientEcs.DescribeTasksAsync(new DescribeTasksRequest
       {
           Cluster = null,
           Include = null,
           Tasks = null
       });
       return string.Empty;
   }

    private async Task<(string imageName, string registry)> BuildImage(string dockerfile)
    {
        Console.WriteLine("BuildImage started");
        var accountId = await GetAccountId();
        var tag = Guid.NewGuid().ToString();
        //todo region static
        var registry = GetRegistry(accountId);
        var imageName = $"{registry}/{RepoName}:{tag}";
        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("build")
                .Add("-t")
                .Add(imageName)
                .Add(dockerfile)
            )
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();
        var stdErr = result.StandardError;
        if (stdErr.Length>0)
            throw new SystemException(stdErr);
        return (imageName,registry);
    }

    private async Task<string> DecodeRegistryLoginTokenToPassword()
    {
        var tokenResponse = await ClientEcr.GetAuthorizationTokenAsync(new GetAuthorizationTokenRequest());
        var decodeToken = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(tokenResponse.AuthorizationData.First().AuthorizationToken))
            .Split(":")[1];
        return decodeToken;
    }

    private static async Task LoginToRegistry(string decodeToken, string registry)
    {
        Console.WriteLine("RegistryLogin started");
        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("login")
                .Add("--username")
                .Add("AWS")
                .Add("--password")
                .Add(decodeToken)
                .Add(registry)
            )
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();
    }

    private async Task DeployImageToEcr(string imageName)
    {
        Console.WriteLine("DeployImageToEcr started...");
        var result = await Cli.Wrap("docker")
            .WithArguments(args => args
                .Add("push")
                .Add(imageName)
            )
            .WithValidation(CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();
        var stdOut = result.StandardOutput;
        var stdErr = result.StandardError;
        // if (stdErr.Length>0)
        // {
        //     throw new SystemException(stdErr);
        // }
        Console.WriteLine(stdOut);
    }
    
    private async Task<RegisterTaskDefinitionResponse> RegisterTaskDefinition(string image,string containerName)
    {
        Console.WriteLine("RegisterTaskDefinition....");
        var taskDefinitionResponse = await ClientEcs.RegisterTaskDefinitionAsync(new RegisterTaskDefinitionRequest
        {
            ContainerDefinitions = new List<ContainerDefinition>()
            {
                new ContainerDefinition
                {
                    Essential = true,
                    Image = image,
                    Name = containerName,
                    PortMappings = new List<PortMapping>()
                    {
                        new PortMapping
                        {
                            ContainerPort = 80,
                            HostPort = 80,
                            Protocol = TransportProtocol.Tcp,
                        }
                    },
                }
            },
            Cpu = "512",
            Memory = "1024",
            Family = $"{ProjectName}",
            NetworkMode = new NetworkMode("awsvpc"),
            RequiresCompatibilities = new List<string>()
            {
                "FARGATE",
                "EC2"
            },
            ExecutionRoleArn = "ecsTaskExecutionRole",
            TaskRoleArn = "ecsTaskExecutionRole",
            Tags = new List<Amazon.ECS.Model.Tag>()
            {
                new Amazon.ECS.Model.Tag
                {
                    Key = "Project",
                    Value = ProjectName
                }
            }
        });
        
        Console.WriteLine($"status: {taskDefinitionResponse.HttpStatusCode}");
        return taskDefinitionResponse;
    }
    private async Task<StackStatus?> GetStackStatus(string stackName)
    {
        try
        {
            var stacksResponse =await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = stackName
            });
            var stackStatus = stacksResponse.Stacks.First().StackStatus;
            return stackStatus;
        }
        catch (Exception e)
        {
            return null;
        }
       
    }
    private async Task DisplayResourcesStatus(string stackName)
    {
        var endStatus = new List<StackStatus>()
        {
            StackStatus.ROLLBACK_IN_PROGRESS,
            StackStatus.CREATE_IN_PROGRESS,
            StackStatus.DELETE_IN_PROGRESS,
            StackStatus.UPDATE_IN_PROGRESS,
        };
        var stackStatus = await GetStackStatus(stackName);
        if (stackStatus is null)
            return;
        
        while(endStatus.Exists(e=>e==stackStatus))
        {
            var eventsResponse = await ClientCformation.DescribeStackResourcesAsync(new DescribeStackResourcesRequest()
            {
                StackName = stackName
            });
            foreach (var resource in eventsResponse.StackResources)
                Console.WriteLine($"{resource.ResourceType}, status = {resource.ResourceStatus}");

            Thread.Sleep(5);
            stackStatus = await GetStackStatus(stackName);
            if (stackStatus is null)
                Console.WriteLine("Stack Status: DELETE_COMPLETE");
            else
            {
                Console.WriteLine("====>");
                Console.WriteLine($"Stack Status: {stackStatus}");
                Console.WriteLine("====>");
            }
            
        }
    }
    private async Task CreateDockerfile(string projectType)
    {
        var url = "https://gist.githubusercontent.com/hamzabouissi/abc0a0bffe61df2c51cc03ed47a6f1ab/raw/a22072086ca54387216eab3c315b05bab9deeeb6";
        switch (projectType)
        {
            case "Fastapi":
                url = $"{url}/fastapi-dockerfile";
                break;
            case "NodeJs":
                url = $"{url}/nodejs-dockerfile";
                break;
            case "AspNetCore":
                break;
        }
        var dockerfileContent = await url.GetStringAsync();
        await File.WriteAllTextAsync("Dockerfile",dockerfileContent);
    }
    private async Task DeleteEcrImages(string repo)
    {
        
        var imagesResponse = await ClientEcr.ListImagesAsync(request: new ListImagesRequest { RepositoryName = repo });
        if (imagesResponse.ImageIds.Count == 0)
            return;
        
        Console.WriteLine("DeleteEcrImages started....");
        await ClientEcr.BatchDeleteImageAsync(new BatchDeleteImageRequest
        {
            ImageIds = imagesResponse.ImageIds,
            RepositoryName = repo
        });
    }
    private async Task<DeleteStackResponse?> DeleteStack(string stackName)
    {
        try
        {
            Console.WriteLine($"Delete Stack {stackName}");
            var deleteServiceStackResponse = await ClientCformation.DeleteStackAsync(new DeleteStackRequest
            {
                StackName = stackName
            });
            var stackStatus = await GetStackStatus(ServiceStackName);
            Console.WriteLine("Deleting Stack On Progress");
            while (stackStatus != null && stackStatus != StackStatus.DELETE_COMPLETE)
            {
                Thread.Sleep(5);
                stackStatus = await GetStackStatus(ServiceStackName);
            }
            Console.WriteLine("Deleting Stack Completed");
            return deleteServiceStackResponse;
        }
        catch (StackNotFoundException)
        {
            Console.WriteLine($"No Stack {stackName}....");
            return null;
        }
                
    }
    private async Task DeleteService()
    {
        var tasksResponse = await ClientEcs.ListTasksAsync(new ListTasksRequest
        {
            Cluster = ClusterName,
        });
        foreach (var task in tasksResponse.TaskArns)
        {
            await ClientEcs.StopTaskAsync(new StopTaskRequest
            {
                Cluster = ClusterName,
                Task = task
            });
        }
        try
        {
            var response = await ClientEcs.DeleteServiceAsync(new DeleteServiceRequest
            {
                Cluster = ClusterName,
                Force = true,
                Service = ServiceName
            });
            Console.WriteLine($"status: {response.HttpStatusCode}");
        }
        catch (ServiceNotFoundException e)
        {
            Console.WriteLine("Service Not Found on Ecs...");
        }
    }
    
    #region Cloudformation
    public async Task CloudformationInit(bool createDockerfile, string projectType)
    {
        if (createDockerfile)
            await CreateDockerfile(projectType);
        var cloudFile = await ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/network.yml");
        var parameters = new List<Parameter>()
        {
            new ()
            {
                ParameterKey = "RepositoryName",
                ParameterValue = RepoName,
            }
        };
        
        try
        {
            
            var response = await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                Parameters = parameters,
                StackName = MainStackName,
                Capabilities = new List<string>()
                {
                    "CAPABILITY_NAMED_IAM"
                },
                TemplateBody = cloudFile,
                TimeoutInMinutes = 5
            });
            
        }
        catch (AlreadyExistsException)
        {
            Console.WriteLine("Update stack...");
            await ClientCformation.UpdateStackAsync(new UpdateStackRequest
            {
            
                DisableRollback = false,
                StackName = MainStackName,
                UsePreviousTemplate = true
            });
        }
        await DisplayResourcesStatus(MainStackName);
        var stackStatus = await GetStackStatus(MainStackName);
        if (stackStatus is null)
            return;
        var describeStacksResponse = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = MainStackName
        });
        var stack = describeStacksResponse.Stacks.First();
        var output = stack.Outputs.Single(o => o.OutputKey == "ExternalUrl");
        Console.WriteLine($"ExternalUrl: {output.OutputValue}");
    }
    public async Task CloudformationDestroy()
    {
        await DeleteEcrImages(RepoName);
        await DeleteService();
        await DeleteStack(ServiceStackName);
        await DeleteStack(MainStackName);
        // await DisplayResourcesStatus(MainStackName);
    }

    public async Task CloudformationDeploy(string dockerfile)
    {
        
        var (image,registry) = await BuildImage(dockerfile);
        var password = await DecodeRegistryLoginTokenToPassword();
        await LoginToRegistry(password,registry);
        await DeployImageToEcr(image);
        
        var cloudFile = await ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/service.yml");
        var parameters = new List<Parameter>()
        {
           new ()
           {
               ParameterKey = "StackName",
               ParameterValue = MainStackName,
           },
           new ()
           {
               ParameterKey = "ServiceName",
               ParameterValue = ServiceName,
           },
           new ()
           {
               ParameterKey = "HealthCheckPath",
               ParameterValue = "/",
           },
           new ()
           {
               ParameterKey = "ImageUrl",
               ParameterValue = image,
           },
           new ()
           {
               ParameterKey="HealthCheckIntervalSeconds",
               ParameterValue="90"
           },
           new ()
           {
               ParameterKey = "Path",
               ParameterValue = "/service_1",
           },
           new ()
           {
               ParameterKey = "Priority",
               ParameterValue = "1",
           },
        };
        try
        {
            var response = await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                Parameters = parameters,
                StackName = ServiceStackName,
                TemplateBody = cloudFile,
                TimeoutInMinutes = 5,


            });
            
        }
        catch (AlreadyExistsException )
        {
            Console.WriteLine($"updating service stack {ServiceStackName}");
            var response = await ClientCformation.UpdateStackAsync(new UpdateStackRequest
            {

                Parameters = parameters,
                StackName = ServiceStackName,
                UsePreviousTemplate = true,
            });
        }
        await DisplayResourcesStatus(ServiceStackName);
    }

    #endregion
   
}
