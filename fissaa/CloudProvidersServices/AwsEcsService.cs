
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Amazon.ECS;
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

    public AwsEcsService(string awsSecretKey,string awsAccessKey, string domainName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        ecsClient = new AmazonECSClient(auth,Region);
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, domainName);
        cloudWatchLogsClient = new AmazonCloudWatchLogsClient(auth,region:Region);
        domainServices = new AwsDomainService(awsSecretKey, awsAccessKey);
        
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

        var baseDomain = string.Join(".",DomainName.Split(".")[^2..]);
        var hostedZoneId = await domainServices.GetHostedZoneId(baseDomain);

        if (createDockerfile && projectType is not null)
            await awsUtilFunctions.CreateDockerfile(projectType);
        
        var image = await ImageDeployment(dockerfile);
        // var image = "182476924183.dkr.ecr.us-east-1.amazonaws.com/django-jooddevops-xyz:1650044588000";
        if (addMonitor)
        {
       
            cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/service-with-monitoring.yml");    
        }
        else
            cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/service.yml");
        var priorityNumber = await awsUtilFunctions.GetListenerRuleNextPriorityNumber(NetworkStackName);
        var parameters = new List<Parameter>()
        {
           new ()
           {
               ParameterKey = "StackName",
               ParameterValue = NetworkStackName,
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
        }
        catch (AlreadyExistsException )
        {
            var priority = parameters.Find(x => x.ParameterKey == "Priority");
            priority.ParameterValue = (int.Parse(priority.ParameterValue)-1).ToString();
            await Update(parameters, cloudFile);
        }
        Console.WriteLine("Waiting Stack");
        await awsUtilFunctions.WaitUntilStackCreatedOrDeleted(ServiceStackName);
        var status = await awsUtilFunctions.GetStackStatus(ServiceStackName);
        AddInfoFile(addMonitor);
        return awsUtilFunctions.StackStatusIsSuccessfull(status) ? Result.Success(status): Result.Failure<StackStatus>("creating app failed") ;
        
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
    
    private async Task Update(List<Parameter> parameters, string cloudFile)
    {
        Console.WriteLine($"updating service stack {ServiceStackName}");
        var response = await ClientCformation.UpdateStackAsync(new UpdateStackRequest
        {
            Parameters = parameters,
            StackName = ServiceStackName,
            TemplateBody = cloudFile,
        });
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

    public async Task<Result> RollBack()
    {
        return Result.Success();
    }
}
