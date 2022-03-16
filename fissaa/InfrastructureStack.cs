using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Vpc = Amazon.CDK.AWS.EC2.Vpc;

namespace fissaa;

public class InfrastructureStack:Stack
{
    public InfrastructureStack(Constructs.Construct scope, string id, IStackProps props ):base(scope,id,props)
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
        var security = new SecurityGroup(this, "SecurtiyGroup", new SecurityGroupProps
        {
            Vpc = vpc,

        });
        var ingress = new CfnSecurityGroupIngress(this, "IngressSecurityGroup", new CfnSecurityGroupIngressProps
        {
            GroupId = security.SecurityGroupId,
            IpProtocol = "tcp",
            FromPort = 80,
            SourceSecurityGroupId = security.SecurityGroupId,
            ToPort = 80
        });
    }
    
}