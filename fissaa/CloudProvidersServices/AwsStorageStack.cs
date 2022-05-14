using Amazon;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.Runtime;
using CSharpFunctionalExtensions;
using fissaa.commands.Templates;

namespace fissaa.CloudProvidersServices;

public class AwsStorageStack
{
    private readonly AmazonCloudFormationClient _clientCformation;
    private string DbStackName(string name)=> $"{name}-db-stack-1";
    private string S3StackName(string name) => $"{name}-s3-stack";
    private RegionEndpoint Region { get; set; } = RegionEndpoint.USEast1;
    private readonly AwsUtilFunctions _awsUtilFunctions;
   

    
    
    public AwsStorageStack(string awsSecretKey,string awsAccessKey)
    {
        
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        _clientCformation = new AmazonCloudFormationClient(auth,Region);
        _awsUtilFunctions = new AwsUtilFunctions(awsSecretKey, awsAccessKey);
    }


    public async Task<Result<DatabaseAuth>> InitDb(DatabaseAuth databaseAuth)
    {
        var parameters = new List<Parameter>()
        {
            new()
            {
                ParameterKey = "DBName",
                ParameterValue = databaseAuth.dbName,
            },
            new()
            {
                ParameterKey = "Engine",
                ParameterValue = databaseAuth.engine,
            },
            new()
            {
                ParameterKey = "MasterUsername",
                ParameterValue = databaseAuth.username,
            },
            new()
            {
                ParameterKey = "MasterUserPassword",
                ParameterValue = databaseAuth.password,
            },
            new()
            {
                ParameterKey = "AllocatedStorage",
                ParameterValue = databaseAuth.storage.ToString(),
            },
        };
        try
        {
            await _clientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DO_NOTHING,
                Parameters = parameters,
                StackName = DbStackName(databaseAuth.dbName),
                TemplateURL = "https://fissaa-cli.s3.amazonaws.com/test/database.yml",
                TimeoutInMinutes = 20
            });
        }
        catch (AlreadyExistsException)
        {
        }
      
        await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(DbStackName(databaseAuth.dbName));
        var stackStatus = await _awsUtilFunctions.GetStackStatus(DbStackName(databaseAuth.dbName));
        var success =  _awsUtilFunctions.StackStatusIsSuccessfull(stackStatus);
        if (success)
        {
            var describeStacksResponse = await _clientCformation.DescribeStacksAsync(new DescribeStacksRequest
            {
                StackName = DbStackName(databaseAuth.dbName)
            });
            
            var stack = describeStacksResponse.Stacks.First();
            var dbHost = stack.Outputs.Single(o => o.OutputKey == "EndpointAddress");
            databaseAuth.dbHost = dbHost.OutputValue;
            return Result.Success(databaseAuth);
        }

        return Result.Failure<DatabaseAuth>("Error while creating database");
    }

    public async Task<List<Output>> DescribeDatabase(string dbName)
    {
        var describeStacksResponse = await _clientCformation.DescribeStacksAsync(new DescribeStacksRequest
        {
            StackName = DbStackName(dbName)
        });
        var stack = describeStacksResponse.Stacks.First();
        return stack.Outputs;
      
    }

    public async Task<Result> InitS3(string bucketName)
    {
        var parameters = new List<Parameter>()
        {
          
            new()
            {
                ParameterKey = "BucketName",
                ParameterValue = bucketName,
            },
        };
        try
        {
            await _clientCformation.CreateStackAsync(new CreateStackRequest
            {
                OnFailure = OnFailure.DELETE,
                Parameters = parameters,
                StackName = S3StackName(bucketName),
                TemplateURL = "https://fissaa-cli.s3.amazonaws.com/test/bucket.yml",
                TimeoutInMinutes = 20
            });
        }
        catch (AlreadyExistsException)
        {
        }
      
        await _awsUtilFunctions.WaitUntilStackCreatedOrDeleted(S3StackName(bucketName));
        var stackStatus = await _awsUtilFunctions.GetStackStatus(S3StackName(bucketName));
        return _awsUtilFunctions.StackStatusIsSuccessfull(stackStatus) ? Result.Success() : Result.Failure("Failed");

    }

    
}