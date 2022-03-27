
using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.EC2;
using Amazon.ECR;
using Amazon.ECS;
using Amazon.IdentityManagement;
using Amazon.Runtime;
using Amazon.SecurityToken;

using Task = System.Threading.Tasks.Task;


namespace fissaa;

public class AwsNetworkStack
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
    public AwsUtilFunctions AwsUtilFunctions { get; set; }
    public AwsNetworkStack(string awsSecretKey,string awsAccessKey, string projectName)
    {

        ProjectName = projectName;
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        
        ClientEc2 = new AmazonEC2Client(credentials:auth,Region);
        ClientEcs = new AmazonECSClient(credentials:auth,Region);
        ClientEcr = new AmazonECRClient(credentials:auth,Region);
        ClientIam = new AmazonIdentityManagementServiceClient(auth,Region);
        StsClient = new AmazonSecurityTokenServiceClient(auth,Region);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);


        AwsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, projectName);


    }
    

    #region Cloudformation
    public async Task CloudformationInit(bool createDockerfile, string projectType)
    {
        if (createDockerfile)
            await AwsUtilFunctions.CreateDockerfile(projectType);
        var cloudFile = await AwsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/network.yml");
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
        await AwsUtilFunctions.DisplayResourcesStatus(MainStackName);
        var stackStatus = await AwsUtilFunctions.GetStackStatus(MainStackName);
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
        await AwsUtilFunctions.DeleteEcrImages(RepoName);
        await AwsUtilFunctions.DeleteService(ClusterName, ServiceName);
        await AwsUtilFunctions.DeleteStack(ServiceStackName);
        await AwsUtilFunctions.DeleteStack(MainStackName);
        // await DisplayResourcesStatus(MainStackName);
    }

    public async Task CloudformationDeploy(string dockerfile)
    {
        
        var (image,registry) = await AwsUtilFunctions.BuildImage(dockerfile,RepoName);
        var password = await AwsUtilFunctions.DecodeRegistryLoginTokenToPassword();
        await AwsUtilFunctions.LoginToRegistry(password,registry);
        await AwsUtilFunctions.DeployImageToEcr(image);
        
        var cloudFile = await AwsUtilFunctions.ExtractTextFromRemoteFile("https://fissaa-cli.s3.amazonaws.com/test/service.yml");
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
        await AwsUtilFunctions.DisplayResourcesStatus(ServiceStackName);
    }

    #endregion
   
}
