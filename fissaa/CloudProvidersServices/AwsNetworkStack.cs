
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Route53.Model;
using Amazon.Runtime;

using Task = System.Threading.Tasks.Task;


namespace fissaa;

public class AwsNetworkStack
{
    public AmazonCloudFormationClient ClientCformation { get; set; }


    private string ProjectName { get; set; }

    public string ServiceStackName => $"{ProjectName}-Service-Stack";
    public string NetworkStackName => "network-stack";
    public string ServiceName => $"{ProjectName}-Service";
    public string ClusterName => $"App-Cluster";
    public string RepoName => ProjectName;
    public readonly RegionEndpoint Region = RegionEndpoint.USEast1;
    private readonly AwsDomainService domainServices;
    public AwsUtilFunctions awsUtilFunctions { get; set; }
    public AwsNetworkStack(string awsSecretKey,string awsAccessKey, string projectName)
    {
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, projectName);
        domainServices = new AwsDomainService(awsSecretKey, awsAccessKey);
        ProjectName = projectName;
    }
    

    #region Cloudformation
    public async Task CloudformationInit(string domain)
    {
        
        var baseDomain = string.Join("-",domain.Split(".")[^2..]);
        var cloudFile = await awsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/network.yml");
        var parameters = new List<Parameter>()
        {
            new ()
            {
                ParameterKey = "DomainCertificateStackName",
                ParameterValue = $"{baseDomain}-certificate-stack",
            }
        };
        
        try
        {
            
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
            
        }
        catch (AlreadyExistsException)
        {
            Console.WriteLine("Update stack...");
            await ClientCformation.UpdateStackAsync(new UpdateStackRequest
            {
            
                DisableRollback = false,
                StackName = NetworkStackName,
                UsePreviousTemplate = true
            });
        }
        await awsUtilFunctions.DisplayResourcesStatus(NetworkStackName);
    }
    public async Task CloudformationDestroy(bool only_app=false)
    {
        
        // await AwsUtilFunctions.DeleteService(ClusterName, ServiceName);
        await awsUtilFunctions.DeleteStack(ServiceStackName);
        await awsUtilFunctions.DeleteEcrImages(RepoName);
        if (!only_app)
            await awsUtilFunctions.DeleteStack(NetworkStackName);
        // await DisplayResourcesStatus(MainStackName);
    }
    
    public async Task CloudformationDeploy(bool createDockerfile,string? projectType, string dockerfile, string domainName)
    {
        if (createDockerfile && projectType is not null)
            await awsUtilFunctions.CreateDockerfile(projectType);
        if (string.IsNullOrEmpty(domainName))
        {
            Console.WriteLine($"{ProjectName} will be considered as subdomain");
            domainName = ProjectName;
        }
        await awsUtilFunctions.CreateReposityIfNotExist(RepoName);
        var (image,registry) = await awsUtilFunctions.BuildImage(dockerfile,RepoName);
        var password = await awsUtilFunctions.DecodeRegistryLoginTokenToPassword();
        await AwsUtilFunctions.LoginToRegistry(password,registry);
        await awsUtilFunctions.DeployImageToEcr(image);

        var baseDomain = string.Join(".",domainName.Split(".")[^2..]);
        var hostedZoneId = await domainServices.GetHostedZoneId(baseDomain);
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
               ParameterValue = domainName,
           },
           new()
           {
               ParameterKey = "HostedZoneId",
               ParameterValue = hostedZoneId,
           }
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
                TemplateBody = cloudFile,
            });
        }
        await awsUtilFunctions.DisplayResourcesStatus(ServiceStackName);
    }

    #endregion
   
}
