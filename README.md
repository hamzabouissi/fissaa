This project is an experiment  of my knowledge, I'd be happy if anyone find it useful.





# Introduction

Fissaa is a CLI that helped me deploy  applications to AWS ECS without the headache of setting-up domain registration,TLS Certification, Load Balancing,Rolling Back and budget monitoring.

Fissaa is a Tunisian word meaning speed-up.
# Prerequisite

## AWS Policies

The policies may seem too open  for some developers, but I didn't focus so much on giving just the required permission for each service.

you can open a pull request if you find it a problem and I will solve it.

Note: to add those policies you need root account.

## AWS Managed Policies

- [AWSCertificateManagerFullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn:aws:iam::aws:policy/AWSCertificateManagerFullAccess)
- [AmazonECS_FullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn:aws:iam::aws:policy/AmazonECS_FullAccess)
- [AmazonRoute53FullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn:aws:iam::aws:policy/AmazonRoute53FullAccess)
- [AmazonS3FullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn:aws:iam::aws:policy/AmazonS3FullAccess)
- [CloudFrontFullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn:aws:iam::aws:policy/CloudFrontFullAccess)
- [AmazonRDSFullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn%3Aaws%3Aiam%3A%3Aaws%3Apolicy%2FAmazonRDSFullAccess)
- [AmazonEC2FullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn%3Aaws%3Aiam%3A%3Aaws%3Apolicy%2FAmazonEC2FullAccess)
- [IAMUserChangePassword](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn%3Aaws%3Aiam%3A%3Aaws%3Apolicy%2FIAMUserChangePassword)
- [AWSCloudFormationFullAccess](https://us-east-1.console.aws.amazon.com/iam/home#/policies/arn%3Aaws%3Aiam%3A%3Aaws%3Apolicy%2FAWSCloudFormationFullAccess)
- 

## Custom Permissions

here is a link for a list of custom permissions you need to add for you user

https://gist.github.com/hamzabouissi/f800a962502c43c1bcff1bee0be3c5db

## Registered Domain

you need to register a domain, if the domain registrar is AWS Route53 it be easy, but in case of any other registrar you need to crate hosted zone with the CLI.

Note: I used to register my domain on **NameCheap** and route with custom dns to **Route53** name servers.

## Developer Environment

Fissaa is currently available on **Linux** and **Windows**.

Fissaa use heavily **Docker** to build,push images of your applications.

## Containerized Application

your project folder must contain a Dockerfile  and the project is running on port 80.

# Installation

Installing is easy as running 2 commands:

## Linux

```bash
wget https://fissaa-cli.s3.amazonaws.com/test/install.sh -o install.sh
```

```bash
sudo sh install.sh linux
```

Congratulation, Fissaa is ready.

# Initialize AWS Credentials

Before deploying your application, you need to initialize your AWS creedentials on your projcet folder

to do this you can run the following command:

```bash
fissaa env
USAGE:
    fissaa env [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    init <aws-secret-key> <aws-access-key>
```

Note: be sure this AWS credentials belongs to an user who have the required policies.

# App Commands

![fissaa-deployment-commands](https://user-images.githubusercontent.com/20321838/171491938-e3683ec1-1f7f-430b-beeb-52c08f9c4163.png)

## Deploy

Fissaa provide a simple one command to deploy your containerized application:

```bash
fissaa infrastructure yourdomain.com deploy --help
USAGE:
    fissaa infrastructure deploy [OPTIONS]

OPTIONS:
    -h, --help               Prints help information                  
        --environment        Environment are: Dev, Prod                      
        --add-monitor        Add AWS X-Ray to track your user requests
        --dockerfile-path 
```

![fissaa-deploy-demo](https://user-images.githubusercontent.com/20321838/171491733-95b16513-d72e-4bf1-9e4d-dd5632b15d9a.png)




## Update 

updating your deployed application is the same way as deploying it, you just run the same command and it will update your code

```bash
fissaa infrastructure yourdomain.com deploy
```



# Monitoring

Fissaa can integrate a monitoring service(AWS X-Ray) with your deployed application, you need to integrate AWS X-RAY sdk first in your application code.

to deploy and add monitoring, you can run the following command:

```bash
fissaa infrastructure yourdomain.com deploy --add-monitor
```
![fissaa-deploy-add-monitor](https://user-images.githubusercontent.com/20321838/171491792-6d3c0875-bf2a-435d-b3c7-f841cc91accf.png)



## Logging

you can see your application logs by running the following command :

```bash
fissaa infrastructure yourdomain.com logs --help
USAGE:
    fissaa infrastructure logs [OPTIONS]

OPTIONS:
    -h, --help          Prints help information     
        --start-date    format yyyy/MM/dd HH:mm:ss  
        --hour          hour must be between 0 and 5
```


![fissaa-logs-display](https://user-images.githubusercontent.com/20321838/171492161-971002dd-3237-4d49-a054-1e30cc0a7ee1.png)



## Create 5xx Error Alert

```bash
fissaa infrastructure yourdomain.com add-alarm --email <email>
```

You will receive a subscription request on email, after accepting it, if your server hit **10 5xx request within 5 minute** you will get an email like this one.



![image](https://user-images.githubusercontent.com/20321838/171434336-bd7c7bfa-610e-4b1f-882b-e22c9dc049c4.png)

# Rolling Back

## List previous deployed versions

```bash
fissaa infrastructure yourdomain.com rollback list-image-tags --help
USAGE:
    fissaa infrastructure rollback list-image-tags [OPTIONS]

OPTIONS:
    -h, --help    Prints help information
```



## Apply Rollback

```bash
fissaa infrastructure yourdomain.com rollback apply --help
USAGE:
    fissaa infrastructure rollback apply [OPTIONS]

OPTIONS:
    -h, --help             Prints help information                              
        --latest           rollback to version prior to the current version, if 
                           no version nothing happen                            
        --image-version    Image version for container registry
```

# Budget

## Create a Budget Alarm

If you want to keep track your budget and get notified when you exceed it, you can create an alarm using the following command:

```bash
fissaa budget <domain> create <budget-amount> <budget-amount-limit> <email>
```



## List Application Cost

To list your application cost on a day to day basis, you can see with the following command:

```bash
fissaa budget <domain> cost-list
```
![fissaa-budget-list](https://user-images.githubusercontent.com/20321838/171492334-25da55e9-3716-43a7-85bb-bed5db9aa0f9.png)



# Ready-to-Deploy Template

I created a simple template for sake of simplicity, but I want to create more templates for different cases like : 

- ML
- Gaming
- IOT
- ECommerce

## Ghost Platform

One of the case I got while developing this CLI is I wanted to deploy a Ghost Platform without the headache of setting-up all the prerequisite that ghost need like (RDS, S3, Email Server).

So I created a simple command that can deploy ghost under your specific domain:

```bash
fissaa template use --help
USAGE:
    fissaa template use <template-name> <mail-provider> <mail-email> 
<mail-password> [OPTIONS]

ARGUMENTS:
    <template-name>    template name, available options are: ghost       
    <mail-provider>    mailing service, available options are: sendinblue
    <mail-email>                                                         
    <mail-password>                                                      

OPTIONS:
    -h, --help    Prints help information
```

![fisssaa-template-ghost-deploy](https://user-images.githubusercontent.com/20321838/171491849-d4657296-b2a6-4853-9e96-f95a52b04ed0.png)


# AWS Storage Servies(RDS,S3)

## AWS RDS

to initialize a database, you can run the following command:

Note: database is available to connect to from a deployed application.

```bash
fissaa storage db init --help
USAGE:
    fissaa storage db init <db-type> <db-username> <db-name> <db-password> [OPTIONS]

ARGUMENTS:
    <db-type>        different db type allowed values are:  mysql, mariadb, postgres
    <db-username>                                                                   
    <db-name>                                                                       
    <db-password>                                                                   

OPTIONS:
    -h, --help          Prints help information               
        --db-storage    your database size on GB minumum: 20G
```

/image

## S3 Bucket

```bash
fissaa storage s3 --help
USAGE:
    fissaa storage s3 [OPTIONS] <COMMAND>

OPTIONS:
    -h, --help    Prints help information

COMMANDS:
    init <bucket-name>
```

# Domain Functionalities

## Link a Domain to Route53

if you're using a custom domain registrar, run the following to create Route53 hosted zone then copy paste the name server into your domain registrar custom dns .

here is an example: https://www.namecheap.com/support/knowledgebase/article.aspx/767/10/how-to-change-dns-for-a-domain/

```bash
fissaa domain <domain-name> create 
```



## Add TLS Certification

if you have a domain and want to add a TLS Certification to it, you can just run following command

Note: domain must be registered on Route53 as Hosted Zone 

```bash
fissaa domain <domain-name> add-https 
```

