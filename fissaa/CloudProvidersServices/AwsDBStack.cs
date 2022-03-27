using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.EC2.Model;
using Amazon.Runtime;

namespace fissaa;

public class AwsDBStack
{
    public string ProjectName { get; set; }
    public AmazonCloudFormationClient ClientCformation { get; set; }
    public string DbStackName=> $"{ProjectName}-db-stack-1";
    public string NetworkStackName => $"{ProjectName}-network-stack";
    public RegionEndpoint Region { get; set; } = RegionEndpoint.USEast1;
    public AwsUtilFunctions AwsUtilFunctions { get; set; }
   

    
    
    public AwsDBStack(string awsSecretKey,string awsAccessKey, string projectName)
    {
        
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientCformation = new AmazonCloudFormationClient(auth,Region);
        AwsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey, projectName);
        ProjectName = projectName;
    }


    public async Task init(string storageType, string dbName, string engine, string username, string password,string region="us-east-1")
    {
        switch (storageType)
        {
            case "database":
                await initDb(dbName,engine,username, password);
                break;
        }

    }

    private async Task initDb(string dbName, string engine, string username, string password)
    {
        Console.WriteLine("Creating database may take some time, grab coffee :)");
        var parameters = new List<Parameter>()
        {
            new()
            {
                ParameterKey = "NetworkStackName",
                ParameterValue = NetworkStackName,
            },
            new()
            {
                ParameterKey = "DBName",
                ParameterValue = dbName,
            },
            new()
            {
                ParameterKey = "Engine",
                ParameterValue = engine,
            },
            new()
            {
                ParameterKey = "MasterUsername",
                ParameterValue = username,
            },
            new()
            {
                ParameterKey = "MasterUserPassword",
                ParameterValue = password,
            },
        };
        var stackResponse = await ClientCformation.CreateStackAsync(new CreateStackRequest
        {
            DisableRollback = false,
            Parameters = parameters,
            StackName = DbStackName,
            TemplateURL = "https://fissaa-cli.s3.amazonaws.com/test/database.yml",
            TimeoutInMinutes = 20
        });
        await AwsUtilFunctions.DisplayResourcesStatus(DbStackName);
        var stackStatus = await AwsUtilFunctions.GetStackStatus(DbStackName);
        if (stackStatus is null) //todo Error: Sequence contains no matching element when rollback_complete
            return;
        var describeStacksResponse = await ClientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = DbStackName
        });
        var stack = describeStacksResponse.Stacks.First();
        var endpointAddress = stack.Outputs.Single(o => o.OutputKey == "EndpointAddress");
        var endpointPort = stack.Outputs.Single(o => o.OutputKey == "EndpointPort");
        
        Console.WriteLine($"endpointAddress: {endpointAddress.OutputValue}, port: {endpointPort.OutputValue}, dbName: {dbName}");
    }

    private async Task initS3()
    {
        
    }
}