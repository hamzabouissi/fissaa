using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.ECR;
using Amazon.ECR.Model;
using Amazon.ECS.Model;
using Amazon.ECS;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using CliWrap;
using CliWrap.Buffered;

using ResourceType = Amazon.EC2.ResourceType;
using Tag = Amazon.EC2.Model.Tag;
using Task = System.Threading.Tasks.Task;
using TransportProtocol = Amazon.ECS.TransportProtocol;


namespace fissaa;

public class SimpleStack
{
    private List<TagSpecification>? _tagSpecifications = null;
    private AmazonEC2Client ClientEc2 { get; }
    private AmazonECSClient ClientEcs { get;}
    private AmazonECRClient ClientEcr { get; }
    public AmazonSecurityTokenServiceClient StsClient { get; set; }

    private AmazonIdentityManagementServiceClient ClientIam { get; }

    private IDictionary<string, string> _resources = new Dictionary<string, string>();
    private string ProjectName { get; set; }
    
    public SimpleStack(string awsSecretKey,string awsAccessKey, string projectName)
    {

        ProjectName = projectName;
        var auth = new BasicAWSCredentials(awsAccessKey, awsSecretKey);
        ClientEc2 = new AmazonEC2Client(credentials:auth);
        ClientEcs = new AmazonECSClient(credentials:auth);
        ClientEcr = new AmazonECRClient(credentials:auth);
        ClientIam = new AmazonIdentityManagementServiceClient();
        StsClient = new AmazonSecurityTokenServiceClient(auth);  
       
       

    }

    public async Task<string> GetAccountId()
    {
        var getCallerIdentityResponse = await StsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());
        var accountId = getCallerIdentityResponse.Arn.Split(":")[4];
        return accountId;
    }

    #region UtilFunctions

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

    #endregion
    

    #region ResourceFileFunctions

    public void CreateResourceFile()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonString = JsonSerializer.Serialize(_resources, options);
        File.WriteAllText("infrastructure.json", jsonString);
    }
    public Dictionary<string, string> ReadResourceFile()
    {
        var fileName = "infrastructure.json";
        try
        {
            var jsonString = File.ReadAllText(fileName);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString)!;

        }
        catch (FileNotFoundException exception)
        {
            throw new FileLoadException("infrastructure.json file not exist, you need to run init command first");
        }
        
        
    }
    
    private void RemoveResourceFile()
    {
        Console.WriteLine("Deleting Resources file");
        var fileName = "infrastructure.json";
        File.Delete(fileName);
    }

    #endregion

    #region DestroyFunctions

    public async Task Destroy()
    {
        Console.WriteLine("Destroying....");
        _resources = ReadResourceFile();
        await DeleteService();
        await DestroyCluster();
        await DestroyRoute();
        await DestroyRouteTable();
        Thread.Sleep(3); // this for waiting aws to delete
        await DestroySecurityGroup();
        await DestroySubnet();
        await DestroyGateway();
        await DestroyVpc();
        await DestroyIamRole();
        await DestroyEcr();
        RemoveResourceFile();
        Console.WriteLine("Ended...");
    }

    private async Task DeleteService()
    {
        Console.WriteLine("DeleteService started ");
        _resources.TryGetValue("serviceName", out var id);
        if (id is null)
            return;
        
        _resources.TryGetValue("clusterName", out var clusterName);
        if (clusterName is null)
            return;
        await ClientEcs.DeleteServiceAsync(new DeleteServiceRequest
        {
            Cluster = clusterName,
            Force = true,
            Service = id
        });
    }

    private async Task DestroyRoute()
    {
        Console.WriteLine("DestroyRoute started ");
        _resources.TryGetValue("routeTableId", out var id);
        if (id is null)
            return;
        await ClientEc2.DeleteRouteAsync(new DeleteRouteRequest
        {
            DestinationCidrBlock = "0.0.0.0/0",
            RouteTableId = id,

        });
    }

    private async Task DestroyIamRole()
    {
        Console.WriteLine("DestroyIamRole started");
        _resources.TryGetValue("roleName", out var id);
        if (id is null)
            return;
        
        _resources.TryGetValue("policyArn", out var policyArn);
        if (policyArn is null)
            return;
        await ClientIam.DetachRolePolicyAsync(new DetachRolePolicyRequest
        {
            PolicyArn = policyArn,
            RoleName = id
        });
        await ClientIam.DeleteRoleAsync(new DeleteRoleRequest
        {
            RoleName = id
        });
    }

    private async Task DestroyCluster()
    {
        Console.WriteLine("DestroyCluster started");
        _resources.TryGetValue("clusterName", out var id);
        if (id is null)
            return;
        var response = await ClientEcs.DeleteClusterAsync(new DeleteClusterRequest
        {
            Cluster = id
        });
        Console.WriteLine($"status: {response.HttpStatusCode}");
    }


    private async Task DestroySecurityGroup()
    {
        _resources.TryGetValue("securityGroupId", out var id);
        if (id is null)
            return;
        var response = await ClientEc2.DescribeSecurityGroupsAsync(new DescribeSecurityGroupsRequest
        {
            GroupIds = new List<string>(){id},
        });
        var securityGroup = response.SecurityGroups.FirstOrDefault();
        if (securityGroup != null)
        {
           
            var ipPermissions = securityGroup.IpPermissions;
            if (ipPermissions.Count>0)
            {
                Console.WriteLine("RevokeSecurityGroupIngressAsync started ");
                await ClientEc2.RevokeSecurityGroupIngressAsync(new RevokeSecurityGroupIngressRequest
                {
                    GroupId = id,
                    IpPermissions = ipPermissions,
                });
            }
        }
       
        await ClientEc2.DeleteSecurityGroupAsync(new DeleteSecurityGroupRequest
        {
            GroupId = id,
        });
    }

    private async Task DestroySubnet()
    {
       
        Console.WriteLine("DestroySubnet started ");
        _resources.TryGetValue("subnetId", out var id);
        if (id is null)
            return;
        await ClientEc2.DeleteSubnetAsync(new DeleteSubnetRequest
        {
            SubnetId = id
        });
    }

    private async Task DestroyRouteTable()
    {
        Console.WriteLine("DestroyRouteTable started ");
        _resources.TryGetValue("routeTableId", out var id);
        if (id is null)
            return;
        var associateIdsResponse =  await ClientEc2.DescribeRouteTablesAsync(new DescribeRouteTablesRequest
        {

            RouteTableIds = new List<string>() { id }
        });
        var ids = associateIdsResponse.RouteTables.First().Associations
            .Where(ass=>!ass.Main)
            .Select(ass => ass.RouteTableAssociationId);
        foreach (var associateId in ids )
        {
            Console.WriteLine($"DisassociateRouteTable {associateId}");
            await ClientEc2.DisassociateRouteTableAsync(new DisassociateRouteTableRequest
            {
                AssociationId = associateId
            });
        }
        
        await ClientEc2.DeleteRouteTableAsync(new DeleteRouteTableRequest()
        {
            RouteTableId = id
        });
    }

    private async Task DestroyGateway()
    {
        Console.WriteLine("DestroyGateway started ");
        _resources.TryGetValue("vpcId", out var vpcId);
        if (vpcId is null)
        {
            Console.WriteLine($"{vpcId} not found");
        }
        _resources.TryGetValue("internetGatewayId", out var id);
        if (id is null)
            return;
        var detachInternetGatewayResponse= await ClientEc2.DetachInternetGatewayAsync(new DetachInternetGatewayRequest
        {
            InternetGatewayId = id,
            VpcId = vpcId
        });
        var response = await ClientEc2.DeleteInternetGatewayAsync(new DeleteInternetGatewayRequest
        {
            InternetGatewayId = id
        });
        Console.WriteLine(response.HttpStatusCode);
    }

    private async Task DestroyVpc()
    {
        Console.WriteLine("DestroyVpc started ");
        _resources.TryGetValue("vpcId", out var vpcId);
        if (vpcId is null)
            return;
        await ClientEc2.DeleteVpcAsync(new DeleteVpcRequest
        {
            VpcId = vpcId
        });
    }

    private async Task DestroyEcr()
    {
        await ClientEcr.DeleteRepositoryAsync(new DeleteRepositoryRequest
        {
            RepositoryName =ProjectName
        });
    }

    #endregion

    #region InitlializationFunctions
   
    public async Task Init()
    {
        await CreateRole();
        var vpcResponse = await CreateVpc();
        var gatewayResponse = await CreateInternetGateway(vpcResponse.Vpc.VpcId);
        var routeTableResponse  =  await CreateRouteTable(vpcResponse.Vpc.VpcId);
        await CreateRoute(gatewayResponse.InternetGateway.InternetGatewayId,routeTableResponse.RouteTable.RouteTableId);
        await CreateSubnet(vpcResponse.Vpc.VpcId,routeTableResponse.RouteTable.RouteTableId);
        var securityGroupResponse = await CreateSecurityGroup(vpcResponse.Vpc.VpcId);
        await CreateSecurityGroupIngress(securityGroupResponse.GroupId);
        await CreateCluster();
        await CreateEcr();
        CreateResourceFile();
    }

    private async Task CreateRole()
    {
        Console.WriteLine("CreateRole started ...");
        var accountId = await GetAccountId();
        var role = new Dictionary<string, List<Dictionary<string,object>>>();
        var statements = new Dictionary<string,object>();
        statements.Add("Effect","Allow");
        statements.Add("Action","sts:AssumeRole");
        statements.Add("Principal",new Dictionary<string,string>
        {
            {"Service", "ecs-tasks.amazonaws.com"}
        });
        role.Add("Statement", new List<Dictionary<string,object>>()
        {
            statements
        });
        
        var options = new JsonSerializerOptions { WriteIndented = true };
        var jsonString = JsonSerializer.Serialize(role, options);
        var response = await ClientIam.CreateRoleAsync(new()
        {
            AssumeRolePolicyDocument = jsonString,
            RoleName = "executionRole",
            Description = "execution Role for ECS"
        });
        if( response.HttpStatusCode != HttpStatusCode.OK)
            Console.WriteLine("Role Not Created");
        Console.WriteLine($"role created result: {response.HttpStatusCode}");
        var attachRolePolicyResponse = await ClientIam.AttachRolePolicyAsync(new AttachRolePolicyRequest
        {
            PolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy",
            RoleName = "executionRole"
        });
        _resources.Add("roleName","executionRole");
        _resources.Add("policyArn","arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy");
        Console.WriteLine($"role attach result: {attachRolePolicyResponse.HttpStatusCode}");

    }

    private async Task CreateCluster()
    {
        Console.WriteLine("CreateCluster started...");
        var response = await ClientEcs.CreateClusterAsync(new CreateClusterRequest
        {
            ClusterName = $"{ProjectName}-Cluster",
            Tags = new List<Amazon.ECS.Model.Tag>
            {
                new()
                {
                    Key = "Project",
                    Value = ProjectName
                }
            }
        });
        if (response.HttpStatusCode==HttpStatusCode.OK)
        {
            _resources.Add("clusterName",response.Cluster.ClusterName);
        }
        Console.WriteLine($"status: {response.HttpStatusCode}");
    }


    private async Task CreateEcr()
    {
        Console.WriteLine("Create Ecr ...");
        var  eCreateRepositoryResponse = await ClientEcr.CreateRepositoryAsync(new CreateRepositoryRequest
        {
            RepositoryName = ProjectName,
        });
        if (eCreateRepositoryResponse.HttpStatusCode == HttpStatusCode.OK)
        {
            _resources.Add("RepositoryArn",eCreateRepositoryResponse.Repository.RepositoryArn);
        }
        Console.WriteLine($"status {eCreateRepositoryResponse.HttpStatusCode}");
    }
    private async Task<AuthorizeSecurityGroupIngressResponse> CreateSecurityGroupIngress(string groupId)
    {
        Console.WriteLine("Add Ingress Port");
        var response =  await ClientEc2.AuthorizeSecurityGroupIngressAsync(new AuthorizeSecurityGroupIngressRequest
        {
            GroupId = groupId,
            IpPermissions = new List<IpPermission>()
            {
                new IpPermission
                {

                    FromPort = 80,
                    IpProtocol = "tcp",
                    Ipv4Ranges = new List<IpRange>()
                    {
                        new IpRange
                        {
                            CidrIp = "0.0.0.0/0",
                            Description = "Allow All HTTP request"
                        }
                    },
                    ToPort = 80,

                }
            },
            
            TagSpecifications = _tagSpecifications
        });
        Console.WriteLine($"status:{response.HttpStatusCode}");
        return response;
    }
    private async Task<CreateSecurityGroupResponse> CreateSecurityGroup(string vpcId)
    {
        Console.WriteLine("CreateSecurityGroup started....");
        var securityGroupResponse =  await ClientEc2.CreateSecurityGroupAsync(new CreateSecurityGroupRequest
        {
            Description = "SecurityGroup", 
            GroupName = $"{ProjectName}-SecurityGroup",
            TagSpecifications = CreateTag(ResourceType.SecurityGroup),
            VpcId = vpcId
        });
        
        if (securityGroupResponse.HttpStatusCode == HttpStatusCode.OK)
            _resources.Add("securityGroupId",securityGroupResponse.GroupId);
        return securityGroupResponse;
    }

    private async Task<CreateRouteTableResponse> CreateRouteTable(string vpcId)
    {
        Console.WriteLine("CreateRouteTable started....");
        var routeTableResponse =  await ClientEc2.CreateRouteTableAsync(new CreateRouteTableRequest
        {
            TagSpecifications = CreateTag(ResourceType.RouteTable),
            VpcId = vpcId
        });
        if (routeTableResponse.HttpStatusCode == HttpStatusCode.OK)
        {
            _resources.Add("routeTableId",routeTableResponse.RouteTable.RouteTableId);
        }
        
        return routeTableResponse;
    }

    private async Task<CreateRouteResponse> CreateRoute(string gatewayId,string routeTableId)
    {
        Console.WriteLine("CreateRoute started ....");
        var routeResponse =  await ClientEc2.CreateRouteAsync(new CreateRouteRequest
        {
            DestinationCidrBlock = "0.0.0.0/0",
            GatewayId = gatewayId,
            RouteTableId = routeTableId,

        });
        return routeResponse;
    }

    private async Task<CreateInternetGatewayResponse> CreateInternetGateway(string vpcId)
    {
        Console.WriteLine("CreateInternetGateway started....");
        var internetGatwayResponse =  await ClientEc2.CreateInternetGatewayAsync(new CreateInternetGatewayRequest
        {
            TagSpecifications = CreateTag(ResourceType.InternetGateway)
        });
        if (internetGatwayResponse.HttpStatusCode == HttpStatusCode.OK)
        {
            _resources.Add("internetGatewayId", internetGatwayResponse.InternetGateway.InternetGatewayId);
            await ClientEc2.AttachInternetGatewayAsync(new AttachInternetGatewayRequest
            {
                InternetGatewayId = internetGatwayResponse.InternetGateway.InternetGatewayId,
                VpcId = vpcId
            });
        }

        return internetGatwayResponse;
    }

    private async Task<CreateSubnetResponse> CreateSubnet(string vpcId,string routeTableId)
    {
        Console.WriteLine("CreateSubnet started...");
        var subnet= await ClientEc2.CreateSubnetAsync(new CreateSubnetRequest
        {
            AvailabilityZone = "us-east-1a",
            CidrBlock = "10.0.0.0/24",
            TagSpecifications = CreateTag(ResourceType.Subnet),
            VpcId = vpcId,

        });
        if (subnet.HttpStatusCode == HttpStatusCode.OK)
        {
            _resources.Add("subnetId",subnet.Subnet.SubnetId);
        }
        Console.WriteLine("AssociateSubnet started...");
        await ClientEc2.AssociateRouteTableAsync(new AssociateRouteTableRequest
        {
            RouteTableId = routeTableId,
            SubnetId = subnet.Subnet.SubnetId
        });
        Console.WriteLine("AssociateSubnet ended...");
       
        return subnet;
    }

    private async Task<CreateVpcResponse> CreateVpc()
    {
       
        Console.WriteLine("CreateVpc started...");
        var vpc = await ClientEc2.CreateVpcAsync(new CreateVpcRequest
        {
            CidrBlock = "10.0.0.0/16",
            TagSpecifications = CreateTag(ResourceType.Vpc),
            
        });
        if (vpc.HttpStatusCode == HttpStatusCode.OK)
        {
            _resources.Add("vpcId",vpc.Vpc.VpcId);
        }
        Console.WriteLine($"status {vpc.HttpStatusCode}");
        return vpc;
    }
    #endregion

    #region DeployFunctions

    public async Task Deploy(string dockerfile)
    {
        
        _resources = ReadResourceFile();
        _resources.TryGetValue("serviceName", out var serviceName);

        // var (image,registry) = await BuildImage(dockerfile);
        // var password = await DecodeRegistryLoginTokenToPassword();
        // await LoginToRegistry(password,registry);
        // await DeployImageToEcr(image);
        //
        // var taskDefinition = await RegisterTaskDefinition(image, $"{ProjectName}-Container");
        // if (serviceName is null)
        //     await CreateService(_resources["clusterName"], _resources["securityGroupId"],_resources["subnetId"],taskDefinition.TaskDefinition.TaskDefinitionArn);
        // else
        //     await UpdateService(_resources["clusterName"],serviceName, taskDefinition.TaskDefinition.TaskDefinitionArn);
        
        var ip = await GetEcsServiceIp();
        Console.WriteLine(string.IsNullOrEmpty(ip) ? "couldn't find service ip" : $"Ip:{ip}");
        // CreateResourceFile();
    }

    private async Task<string> GetEcsServiceIp()
    {
        _resources.TryGetValue("clusterName", out var clusterName);
        _resources.TryGetValue("taskArn", out var taskId);

        if (clusterName is null || taskId is null)
        {
            throw new SystemException("Either clusterName or taskId not Exist on infrastructore.json file");
        }
        var response = await ClientEcs.DescribeTasksAsync(new DescribeTasksRequest
        {
            Cluster = clusterName,
            Include = null,
            Tasks = new List<string>(){taskId}
        });
        foreach (var networkInterface in response.Tasks.First().Attachments.Select(attachment => attachment.Details.Single(d => d.Name == "networkInterfaceId")))
        {
            if (networkInterface is null)
                throw new SystemException("No network Interfaces attached Ip found");

            var networkInterfacesResponse = await ClientEc2.DescribeNetworkInterfacesAsync(new DescribeNetworkInterfacesRequest
            {
               
                NetworkInterfaceIds = new List<string>{networkInterface.Value},
            });
            return networkInterfacesResponse.NetworkInterfaces.First().Association.PublicIp;
        } 
        return string.Empty;
    }


    private async Task<(string imageName, string registry)> BuildImage(string dockerfile)
    {
        Console.WriteLine("BuildImage started");
        var accountId = await GetAccountId();
        var tag = Guid.NewGuid().ToString();
        //todo region static
        var registry = GetRegistry(accountId);
        var imageName = $"{registry}/{ProjectName}:{tag}";
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

    private async Task<CreateServiceResponse> CreateService(string cluster,string securitGroup,string subnet, string taskDefinitionArn)
    {
        Console.WriteLine("CreateService started...");
        var serviceName = $"{ProjectName}-Service";
        var serviceResponse = await ClientEcs.CreateServiceAsync(new CreateServiceRequest
        {
            Cluster = cluster,
            DeploymentConfiguration = new DeploymentConfiguration
            {
                MaximumPercent = 200,
                MinimumHealthyPercent = 100
            },
            DeploymentController = new DeploymentController
            {
                Type = DeploymentControllerType.ECS
            },
            DesiredCount = 1,
            LaunchType = LaunchType.FARGATE,
            NetworkConfiguration = new NetworkConfiguration
            {
                AwsvpcConfiguration = new AwsVpcConfiguration
                {
                    AssignPublicIp = AssignPublicIp.ENABLED,
                    SecurityGroups = new List<string>()
                    {
                        securitGroup
                    },
                    Subnets = new List<string>()
                    {
                       subnet
                    }
                }
            },
            SchedulingStrategy = SchedulingStrategy.REPLICA,
            ServiceName = serviceName,
            TaskDefinition = taskDefinitionArn,
            Tags = new List<Amazon.ECS.Model.Tag>()
            {
                new Amazon.ECS.Model.Tag
                {
                    Key = "Project",
                    Value = ProjectName
                }
            }
        });
        if (serviceResponse.HttpStatusCode==HttpStatusCode.OK)
        {
            _resources.Add("serviceName",serviceName);
        }
        Console.WriteLine($"status: {serviceResponse.HttpStatusCode}");
        return serviceResponse;
    }
    private async Task<RegisterTaskDefinitionResponse> RegisterTaskDefinition(string image,string containerName)
    {
        Console.WriteLine("RegisterTaskDefinition....");
        var taskDefinition = await ClientEcs.RegisterTaskDefinitionAsync(new RegisterTaskDefinitionRequest
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
        Console.WriteLine($"status: {taskDefinition.HttpStatusCode}");
        return taskDefinition;
    }
    private async Task UpdateService(string cluster,string serviceName,string taskDefinitionArn)
    {
        Console.WriteLine("UpdateService started...");
        var updateServiceResponse = await ClientEcs.UpdateServiceAsync(new UpdateServiceRequest
        {
            Cluster = cluster,
            Service = serviceName,
            TaskDefinition = taskDefinitionArn,
        });
        Console.WriteLine($"status: {updateServiceResponse.HttpStatusCode}");
    }
    #endregion
}
