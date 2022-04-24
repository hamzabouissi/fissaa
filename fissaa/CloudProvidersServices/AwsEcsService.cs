
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


namespace fissaa;

public class AwsEcsService
{

    private readonly string DomainName;
    private readonly string BaseDomain;
    private readonly string EscapedBaseDomain;
    private readonly string EscapedDomain;

    public string NetworkStackName => $"{EscapedBaseDomain}-network-stack";
    public string ServiceStackName => $"{EscapedDomain}-Service-Stack";
    public string ServiceName => $"{EscapedDomain}-Service";
    public string RepoName => EscapedDomain;
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    public readonly AmazonCloudFormationClient ClientCformation;
    private readonly AwsDomainService domainServices;
    public readonly AwsUtilFunctions awsUtilFunctions;
    private readonly AmazonCloudWatchLogsClient cloudWatchLogsClient;
    private readonly AmazonECSClient ecsClient;
    private readonly AmazonECRClient ecrClient;

    public AwsEcsService(string awsSecretKey,string awsAccessKey, string domainName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        ecsClient = new AmazonECSClient(auth,Region);
        cloudWatchLogsClient = new AmazonCloudWatchLogsClient(auth,region:Region);
        domainServices = new AwsDomainService(awsSecretKey, awsAccessKey);
        ecrClient = new AmazonECRClient(auth, region: Region);
        
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, domainName);

        
        DomainName = domainName;
        BaseDomain = string.Join(".",DomainName.Split(".")[^2..]);
        EscapedBaseDomain = string.Join("-",DomainName.Split(".")[^2..]);
        EscapedDomain = DomainName.Replace(".", "-");
    }
    
    
   
    public async Task Destroy()
    {
        await awsUtilFunctions.DeleteStack(ServiceStackName);
        await awsUtilFunctions.DeleteEcrImages(RepoName);
    }

    public async Task<Result<StackStatus>> Create(bool createDockerfile, string? projectType, string dockerfile,
        bool addMonitor)
    {
        string cloudFile;

        var baseDomain = string.Join(".", DomainName.Split(".")[^2..]);
        var hostedZoneId = await domainServices.GetHostedZoneId(baseDomain);

        if (createDockerfile && projectType is not null)
            await awsUtilFunctions.CreateDockerfile(projectType);

        var image = await ImageDeployment(dockerfile);
        if (addMonitor)
            cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile(
                "https://fissaa-cli.s3.amazonaws.com/test/service-with-monitoring.yml");
        else
            cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile(
                "https://fissaa-cli.s3.amazonaws.com/test/service.yml");
        var priorityNumber = await awsUtilFunctions.GetListenerRuleNextPriorityNumber(NetworkStackName);
        var parameters = GetEcsCloudformationParameters(image, priorityNumber, hostedZoneId);
        try
        {
            Console.WriteLine("Start Deploying");
            await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DO_NOTHING,
                Parameters = parameters,
                StackName = ServiceStackName,
                TemplateBody = cloudFile,
                TimeoutInMinutes = 30,
                Tags = new List<Tag>()
                {
                    new()
                    {
                        Key = "app-domain",
                        Value = BaseDomain
                    }

                }
            });
            Console.WriteLine("Waiting Stack");
            await awsUtilFunctions.WaitUntilStackCreatedOrDeleted(ServiceStackName);
            var status = await awsUtilFunctions.GetStackStatus(ServiceStackName);
            AddInfoFile(addMonitor);
            return awsUtilFunctions.StackStatusIsSuccessfull(status)
                ? Result.Success(status)
                : Result.Failure<StackStatus>("creating app failed");
        }
        catch (AlreadyExistsException)
        {
            var result = await Update(image);
            return result;
        }
        

}

    private List<Parameter> GetEcsCloudformationParameters(string image, int priorityNumber, string hostedZoneId)
    {
        var parameters = new List<Parameter>()
        {
            new()
            {
                ParameterKey = "StackName",
                ParameterValue = NetworkStackName,
            },
            new()
            {
                ParameterKey = "ServiceName",
                ParameterValue = ServiceName,
            },
            new()
            {
                ParameterKey = "HealthCheckPath",
                ParameterValue = "/",
            },
            new()
            {
                ParameterKey = "ImageUrl",
                ParameterValue = image,
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
                ParameterValue = DomainName,
            },
            new()
            {
                ParameterKey = "HostedZoneId",
                ParameterValue = hostedZoneId,
            }
        };
        return parameters;
    }

    public void AddInfoFile(bool addMonitor)
    {
        Directory.CreateDirectory("./.fissaa");
        var info = $"ServiceMap https://us-east-1.console.aws.amazon.com/cloudwatch/home?region={Region.SystemName}#xray:service-map/map\n";
        File.WriteAllText("./.fissaa/links",info);
        info = $"HttpRequestTraces https://us-east-1.console.aws.amazon.com/cloudwatch/home?region={Region.SystemName}#xray:traces/query\n";
        File.AppendAllText("./.fissaa/links", info);
    }
    public async Task ListLogs(string startDate,int hour)
    {
        try
        {
            var logStreamsResponse = await cloudWatchLogsClient.DescribeLogStreamsAsync(new DescribeLogStreamsRequest
            {

                LogGroupName = ServiceName,
                LogStreamNamePrefix = ServiceName,

            });
            var logStreamName = logStreamsResponse.LogStreams.Last().LogStreamName;
            DateTime.TryParse(startDate, out var parsedStartDate);
            Console.WriteLine($"Logs from {parsedStartDate} => {parsedStartDate.AddHours(hour)} ");
            var logsResponse = await cloudWatchLogsClient.GetLogEventsAsync(new GetLogEventsRequest
            {
                StartTime =  parsedStartDate,
                EndTime =parsedStartDate.AddHours(hour),
                Limit = 500,
                LogGroupName = ServiceName,
                LogStreamName = logStreamName,
                StartFromHead = false,


            });
            foreach (var logEvent in logsResponse.Events)
                Console.WriteLine(logEvent.Message);

        }
        catch (ResourceNotFoundException e)
        {
            throw new ResourceNotFoundException("Domain name not found");
        }

       
       
    }
    
    private async Task<Result<StackStatus>> Update(string image)
    {
        Console.WriteLine($"updating service stack {ServiceStackName}");
        
        var baseDomain = string.Join(".",DomainName.Split(".")[^2..]);
        var hostedZoneId = await domainServices.GetHostedZoneId(baseDomain);
        
        var priorityNumber = await awsUtilFunctions.GetListenerRuleNextPriorityNumber(NetworkStackName) - 1;
        var parameters = GetEcsCloudformationParameters(image, priorityNumber, hostedZoneId);
        
        var response = await ClientCformation.UpdateStackAsync(new UpdateStackRequest
        {
            Parameters = parameters,
            StackName = ServiceStackName,
            UsePreviousTemplate = true
        });
      
        Console.WriteLine("Waiting Stack");
        await awsUtilFunctions.WaitUntilStackCreatedOrDeleted(ServiceStackName);
        var status = await awsUtilFunctions.GetStackStatus(ServiceStackName);
        return awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure<StackStatus>("updating app failed") ;
    }

    private async Task<string> ImageDeployment(string dockerfile)
    {
        await awsUtilFunctions.CreateReposityIfNotExist(RepoName);
        Console.WriteLine("Build Image");
        var (image, registry) = await awsUtilFunctions.BuildImage(dockerfile, RepoName);
        var password = await awsUtilFunctions.DecodeRegistryLoginTokenToPassword();
        await AwsUtilFunctions.LoginToRegistry(password, registry);
        Console.WriteLine("Deploy Image");
        await awsUtilFunctions.DeployImageToEcr(image);
        Console.WriteLine("Deploy Image Ended");
        return image;
    }

    public async Task<Result> RollBackApply(bool? latest, string imageVersion)
    {
        var accountId = await awsUtilFunctions.GetAccountId();
        var image = $"{accountId}.dkr.ecr.{Region.SystemName}.amazonaws.com/{RepoName}:{imageVersion}";
        var updateResult = await Update(image);
        return updateResult;

    }
    
    public async Task RollBackList()
    {
        var images = await ecrClient.DescribeImagesAsync(new DescribeImagesRequest
        {
            RepositoryName = RepoName
        });
        foreach (var image in images.ImageDetails.OrderByDescending(p=>p.ImagePushedAt))
        {
            Console.WriteLine($"DateTime: {image.ImagePushedAt}, ImageTag: {string.Join(' ',image.ImageTags)} ");
        }
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
