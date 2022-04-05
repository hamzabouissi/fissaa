
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Route53.Model;
using Amazon.Runtime;

using Task = System.Threading.Tasks.Task;


namespace fissaa;

public class AwsNetworkStack
{



    private readonly string DomainName;
    private readonly string BaseDomain;
    private readonly string EscapedBaseDomain;
    private readonly string EscapedDomain;

    public string DomainCertificateStackName=>$"{EscapedBaseDomain}-certificate-stack";
    public string NetworkStackName => $"{EscapedBaseDomain}-network-stack";
    public string ServiceStackName => $"{EscapedDomain}-Service-Stack";
    public string ServiceName => $"{EscapedDomain}-Service";
    public string RepoName => EscapedDomain;
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    public readonly AmazonCloudFormationClient ClientCformation;
    private readonly AwsDomainService domainServices;
    public readonly AwsUtilFunctions awsUtilFunctions;

    public AwsNetworkStack(string awsSecretKey,string awsAccessKey, string domainName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, domainName);
        domainServices = new AwsDomainService(awsSecretKey, awsAccessKey);
        DomainName = domainName;
        BaseDomain = string.Join(".",DomainName.Split(".")[^2..]);
        EscapedBaseDomain = string.Join("-",DomainName.Split(".")[^2..]);
        EscapedDomain = DomainName.Replace(".", "-");
    }
    
    
    #region Cloudformation
    public async Task<StackStatus?> CloudformationInit()
    {
        
        var cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/network.yml");
        var domainStackStatus = await awsUtilFunctions.GetStackStatus(DomainCertificateStackName);
        if (domainStackStatus is null || domainStackStatus != StackStatus.CREATE_COMPLETE)
        {
            Console.WriteLine("Creating Https Certificate...");
            var addHttps = await domainServices.AddHttps(BaseDomain);
            if (addHttps is null || addHttps != StackStatus.CREATE_COMPLETE)
                return addHttps;
        }
        var parameters = new List<Parameter>()
        {
            new ()
            {
                ParameterKey = "DomainCertificateStackName",
                ParameterValue = DomainCertificateStackName,
            }
        };
        
        try
        {
            Console.WriteLine("Create Network Stack...");
            var response = await ClientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                Parameters = parameters,
                StackName = NetworkStackName,
                Capabilities = new List<string>()
                {
                    "CAPABILITY_NAMED_IAM"
                },
                TemplateBody = cloudFile,
                TimeoutInMinutes = 5
            });
            await awsUtilFunctions.DisplayResourcesStatus(NetworkStackName);
        }
        catch (AlreadyExistsException)
        {
          Console.WriteLine("Network Stack Already Created");
        }

        var stackStatus = await awsUtilFunctions.GetStackStatus(NetworkStackName);
        return stackStatus;

    }
    public async Task CloudformationDestroy(bool only_app=false)
    {
        await awsUtilFunctions.DeleteStack(ServiceStackName);
        await awsUtilFunctions.DeleteEcrImages(RepoName);
        if (!only_app)
            await awsUtilFunctions.DeleteStack(NetworkStackName);
        // await DisplayResourcesStatus(MainStackName);
    }
    
    public async Task CloudformationDeploy(bool createDockerfile,string? projectType, string dockerfile)
    {
        var baseDomain = string.Join(".",DomainName.Split(".")[^2..]);
        var hostedZoneId = await domainServices.GetHostedZoneId(baseDomain);

        var status = await CloudformationInit();
        if (status is null ||  (status != StackStatus.CREATE_COMPLETE && status!=StackStatus.UPDATE_COMPLETE))
            return;
        
        if (createDockerfile && projectType is not null)
            await awsUtilFunctions.CreateDockerfile(projectType);
        
        
        var image = await ImageDeployment(dockerfile);
        
        var cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/service.yml");
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
           new ()
           {
               ParameterKey = "HttpsPriority",
               ParameterValue = (priorityNumber+1).ToString(),
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
            Console.WriteLine("Creating App stack");
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
                TemplateBody = cloudFile,
            });
        }
        await awsUtilFunctions.DisplayResourcesStatus(ServiceStackName);
    }

    #endregion
    private async Task<string> ImageDeployment(string dockerfile)
    {
        await awsUtilFunctions.CreateReposityIfNotExist(RepoName);
        var (image, registry) = await awsUtilFunctions.BuildImage(dockerfile, RepoName);
        var password = await awsUtilFunctions.DecodeRegistryLoginTokenToPassword();
        await AwsUtilFunctions.LoginToRegistry(password, registry);
        await awsUtilFunctions.DeployImageToEcr(image);
        return image;
    }
}
