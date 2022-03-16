using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Protocol = Amazon.CDK.AWS.ECS.Protocol;

namespace fissaa;

public class FargateStack:Stack
{
    public FargateStack(Constructs.Construct scope, string id, IStackProps props ):base(scope,id,props)
    {
     
        var vpc = new Vpc(this,"TestVpc", new VpcProps
        {
            Cidr = "10.0.0.0/16",
            NatGateways = 1,
            MaxAzs = 1,
            SubnetConfiguration = new ISubnetConfiguration[]
            {
                new SubnetConfiguration
                {
                    Name="public-subnet-1",
                    CidrMask = 24,
                    SubnetType = SubnetType.PUBLIC
                }
            }
        });

        var cluster = new Cluster(this, "MyCluster", new ClusterProps
        {
            Vpc = vpc
        });
        var repo = new Repository(this, "EcrRepo", new RepositoryProps
        {
            RepositoryName = "integration-ratehawk"
        });
        // Create a load-balanced Fargate service and make it public
        var taskDef = new FargateTaskDefinition(this, "FargateTaskDef");
        var containerDefinitionOptions = new ContainerDefinitionOptions
        {
            Image = ContainerImage.FromEcrRepository(repo, "eb846d4e32a6ffe2080afd53908953462bf9e971")
        };
        var portMapping = new PortMapping
        {
            ContainerPort = 80,
            HostPort = 80,
            Protocol = Protocol.TCP
        };
        taskDef.AddContainer("Container",containerDefinitionOptions).AddPortMappings(portMapping);
        var applicationLoadBalancedFargateService = new FargateService(this, "MyFargateService",
            new FargateServiceProps()
            {
                Cluster = cluster,          // Required
                DesiredCount = 1,
                AssignPublicIp = true,
                TaskDefinition = taskDef,
                
            }
        );
    }
}