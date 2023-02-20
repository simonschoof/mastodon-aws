namespace MastodonAwsServices.Config

module Secrets =
    open Amazon.SecretsManager
    open Amazon.SecretsManager.Model
    open System.Threading


    let getSecret (secretName: string) =
        let awsSecretManagerClient = new AmazonSecretsManagerClient()
        let mutable secretValueRequest = GetSecretValueRequest()
        secretValueRequest.SecretId <- secretName


        let asyncSecret =
            async {
                let! result =
                    awsSecretManagerClient.GetSecretValueAsync(secretValueRequest, CancellationToken(false))
                    |> Async.AwaitTask

                return result
            }

        let secretResponse = Async.RunSynchronously(asyncSecret)
        secretResponse.SecretString

module Params =
    open Amazon.SimpleSystemsManagement
    open Amazon.SimpleSystemsManagement.Model
    open System.Threading

    let getParameter (parameterName: string) =
        let client = new AmazonSimpleSystemsManagementClient()
        let mutable parameterRequest = GetParameterRequest()
        parameterRequest.Name <- parameterName

        let asyncParameter =
            async {
                let! result =
                    client.GetParameterAsync(parameterRequest, CancellationToken(false))
                    |> Async.AwaitTask

                return result
            }

        let parameterResponse = Async.RunSynchronously(asyncParameter)
        parameterResponse.Parameter.Value

module Values = 
    open Secrets
    open Params
    open Pulumi
    open Pulumi.Awsx.Ecs.Inputs
    
    let awsConfig = Config("aws");
    
    // Pulumi
    let mastodonResourcePrefix = "mastodon-"
    let prefixMastodonResource resourceNameToPrefix= mastodonResourcePrefix + resourceNameToPrefix  

    // RDS
    let rdsDbMasterPassword= getSecret"mastodon/rds/db-master-password"

    // Mastodon federatiom
    let localDomain = "social.simonschoof.com"
    let singleUserMode = true
    let defaultLocale="en"

    // Mastodon secrets
    // let secretKeyBase= getSecret "mastodon/secrets/secret-key-base"  //rake secret
    // let otpSecret = getSecret "mastodon/secrets/otp-secret" //rake secret 
    // let vapIdPrivateKey = getSecret "mastodon/secrets/vapid-private-key" //mastodon:webpush:generate_vapid_key
    // let vapIdPublicKey = getSecret "mastodon/secrets/vapid-public-key" //mastodon:webpush:generate_vapid_key 

    // Mastodon deployment
    let railsEnv = "production"
    let railsServeStaticFiles = true
    let railsLogLevel = "warn"
    let nodeEnv = "production"

    // Mastodon postgres
    // let dbHost = getParameter "mastodon/postgres/db-host"
    // let dbUser = getParameter "mastodon/postgres/db-user"
    // let dbName = getParameter "mastodon/postgres/db-name"
    // let dbPass = getSecret "mastodon/postgres/db-pass"
    
    // Mastodon redis
    // let redisHost = getParameter "mastodon/redis/redis-host"

    // Mastodon email
    // let stmpServer = getParameter "mastodon/mail/smtp-server"
    // let smtpPort = getParameter "mastodon/mail/smtp-port"
    // let smtpLogin = getParameter "mastodon/mail/smtp-login"
    // let smtpPassword = getSecret "mastodon/mail/smtp-password"
    // let smtpFromAddress = getParameter "mastodon/mail/smtp-from-address"
    // let smtpDomain= "social.simonschoof.com"
    // SMTP_DELIVERY_METHOD
    // SMTP_AUTH_METHOD
    // SMTP_CA_FILE
    // SMTP_OPENSSL_VERIFY_MODE
    // SMTP_ENABLE_STARTTLS_AUTO
    // SMTP_ENABLE_STARTTLS
    // SMTP_TLS
    // SMTP_SSL

    // Mastodon Amazon S3 and compatible
    // let s3AliasHost = "mastodonmedia.simonschoof.com"
    // let s3Enabled= true
    // let s3Bucket =  getParameter "mastodon/s3/bucket" -> get directly from bucket
    // let awsAccessKeyId = getSecret "mastodon/s3/aws-access-key-id"
    // let awsSecretAccessKey = getSecret "mastodon/s3/aws-access-key-id"
    // let s3Region = awsConfig.Require("region")
    // let s3Protocol = "HTTPS"
    // let s3Hostname = getParameter "mastodon/s3/hostname" -> get directly from bucket

    // Mastodon other
    let skipPostDeploymentMigrations = true

    // let mastodonContainerEnvVariables  =[
    //     TaskDefinitionKeyValuePairArgs(Name = "LOCAL_DOMAIN", Value = localDomain);
    //     TaskDefinitionKeyValuePairArgs(Name = "SINGLE_USER_MODE", Value = singleUserMode);
    //     TaskDefinitionKeyValuePairArgs(Name = "DEFAULT_LOCALE", Value = defaultLocale);
    //     TaskDefinitionKeyValuePairArgs(Name = "SECRET_KEY_BASE", Value = secretKeyBase);
    //     TaskDefinitionKeyValuePairArgs(Name = "OTP_SECRET", Value = otpSecret);
    //     TaskDefinitionKeyValuePairArgs(Name = "VAPID_PRIVATE_KEY", Value = vapIdPrivateKey);
    //     TaskDefinitionKeyValuePairArgs(Name = "VAPID_PUBLIC_KEY", Value = vapIdPublicKey);
    //     TaskDefinitionKeyValuePairArgs(Name = "RAILS_ENV", Value = railsEnv);
    //     TaskDefinitionKeyValuePairArgs(Name = "RAILS_SERVE_STATIC_FILES", Value = railsServeStaticFiles);
    //     TaskDefinitionKeyValuePairArgs(Name = "RAILS_LOG_LEVEL", Value = railsLogLevel);
    //     TaskDefinitionKeyValuePairArgs(Name = "NODE_ENV", Value = nodeEnv);
    //     TaskDefinitionKeyValuePairArgs(Name = "DB_HOST", Value = dbHost);
    //     TaskDefinitionKeyValuePairArgs(Name = "DB_USER", Value = dbUser);
    //     TaskDefinitionKeyValuePairArgs(Name = "DB_NAME", Value = dbName);
    //     TaskDefinitionKeyValuePairArgs(Name = "DB_PASS", Value = dbPass);
    //     TaskDefinitionKeyValuePairArgs(Name = "REDIS_HOST", Value = redisHost);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_SERVER", Value = stmpServer);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_PORT", Value = smtpPort);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_LOGIN", Value = smtpLogin);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_PASSWORD", Value = smtpPassword);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_FROM_ADDRESS", Value = smtpFromAddress);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_DOMAIN", Value = smtpDomain);
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_DELIVERY_METHOD", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_AUTH_METHOD", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_CA_FILE", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_OPENSSL_VERIFY_MODE", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_ENABLE_STARTTLS_AUTO", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_ENABLE_STARTTLS", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_TLS", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "SMTP_SSL", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "CDN_HOST", Value = "");
    //     TaskDefinitionKeyValuePairArgs(Name = "S3_ALIAS_HOST", Value = s3AliasHost);
    //     TaskDefinitionKeyValuePairArgs(Name = "S3_ENABLED", Value = s3Enabled);
    //     TaskDefinitionKeyValuePairArgs(Name = "S3_BUCKET", Value = s3Bucket); // use from bucket directly
    //     TaskDefinitionKeyValuePairArgs(Name = "AWS_ACCESS_KEY_ID", Value = awsAccessKeyId);
    //     TaskDefinitionKeyValuePairArgs(Name = "AWS_SECRET_ACCESS_KEY", Value = awsSecretAccessKey);
    //     TaskDefinitionKeyValuePairArgs(Name = "S3_REGION", Value = s3Region);
    //     TaskDefinitionKeyValuePairArgs(Name = "S3_PROTOCOL", Value = s3Protocol);
    //     TaskDefinitionKeyValuePairArgs(Name = "S3_HOSTNAME", Value = s3Hostname); // use from bucket directly
    //     TaskDefinitionKeyValuePairArgs(Name = "SKIP_POST_DEPLOYMENT_MIGRATIONS", Value = skipPostDeploymentMigrations)
    // ]

    
