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
    open Pulumi.FSharp
    
    let awsConfig = Config("aws");
    
    // Pulumi
    let mastodonResourcePrefix = "mastodon-"
    let prefixMastodonResource resourceNameToPrefix= mastodonResourcePrefix + resourceNameToPrefix  

    // RDS
    let rdsDbMasterPassword= getSecret"mastodon/rds/db-master-password"

    // Mastodon federatiom
    let localDomain = "social.simonschoof.com"
    let singleUserMode = "true"
    let defaultLocale="en"
    let alternateDomains = "" 

    // Mastodon secrets
    let secretKeyBase= getSecret "mastodon/secrets/secret-key-base"
    let otpSecret = getSecret "mastodon/secrets/otp-secret"
    let vapIdPrivateKey = getSecret "mastodon/secrets/vapid-private-key"
    let vapIdPublicKey = getSecret "mastodon/secrets/vapid-public-key"

    // Mastodon deployment
    let railsEnv = "production"
    let railsServeStaticFiles = "true"
    let railsLogLevel = "warn"
    let nodeEnv = "production"

    // Mastodon postgres
    let dbHost = getParameter "/mastodon/postgres/db-host"
    let dbUser = getParameter "/mastodon/postgres/db-user"
    let dbName = getParameter "/mastodon/postgres/db-name"
    let dbPass = getSecret "mastodon/postgres/db-pass"
    
    // Mastodon redis
    let redisHost = getParameter "/mastodon/redis/redis-host"

    // Mastodon email
    let stmpServer = getParameter "/mastodon/mail/smtp-server"
    let smtpPort = getParameter "/mastodon/mail/smtp-port"
    let smtpLogin = getParameter "/mastodon/mail/smtp-login"
    let smtpPassword = getSecret "mastodon/mail/smtp-password"
    let smtpFromAddress = getParameter "/mastodon/mail/smtp-from-address"
    let smtpDomain= "social.simonschoof.com"
    let smtpAuthMethod = "plain"
    let smtpOpenSslVerifyMode = "none"
    let smtpEnableStarttls = "auto" 

    // Mastodon Amazon S3 and compatible
    let s3AliasHost = "mastodonmedia.simonschoof.com"
    let s3Enabled= "true"
    let s3Bucket =  getParameter "/mastodon/s3/bucket"
    let awsAccessKeyId = getSecret "mastodon/s3/aws-access-key-id"
    let awsSecretAccessKey = getSecret "mastodon/s3/aws-secret-access-key"
    let s3Region = awsConfig.Require("region")
    let s3Protocol = "HTTPS"
    let s3Hostname = getParameter "/mastodon/s3/hostname"

    // Mastodon other
    let skipPostDeploymentMigrations = "true"

    let mastodonContainerEnvVariables  = inputList [
        input (TaskDefinitionKeyValuePairArgs(Name = "LOCAL_DOMAIN", Value = localDomain));
        input (TaskDefinitionKeyValuePairArgs(Name = "SINGLE_USER_MODE", Value = singleUserMode));
        input (TaskDefinitionKeyValuePairArgs(Name = "DEFAULT_LOCALE", Value = defaultLocale));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "SECRET_KEY_BASE", Value = secretKeyBase)));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "OTP_SECRET", Value = otpSecret)));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "VAPID_PRIVATE_KEY", Value = vapIdPrivateKey)));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "VAPID_PUBLIC_KEY", Value = vapIdPublicKey)));
        input (TaskDefinitionKeyValuePairArgs(Name = "RAILS_ENV", Value = railsEnv));
        input (TaskDefinitionKeyValuePairArgs(Name = "RAILS_SERVE_STATIC_FILES", Value = railsServeStaticFiles));
        input (TaskDefinitionKeyValuePairArgs(Name = "RAILS_LOG_LEVEL", Value = railsLogLevel));
        input (TaskDefinitionKeyValuePairArgs(Name = "NODE_ENV", Value = nodeEnv));
        input (TaskDefinitionKeyValuePairArgs(Name = "DB_HOST", Value = dbHost));
        input (TaskDefinitionKeyValuePairArgs(Name = "DB_USER", Value = dbUser));
        input (TaskDefinitionKeyValuePairArgs(Name = "DB_NAME", Value = dbName));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "DB_PASS", Value = dbPass)));
        input (TaskDefinitionKeyValuePairArgs(Name = "REDIS_HOST", Value = redisHost));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_SERVER", Value = stmpServer));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_PORT", Value = smtpPort));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_LOGIN", Value = smtpLogin));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "SMTP_PASSWORD", Value = smtpPassword)));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_FROM_ADDRESS", Value = smtpFromAddress));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_DOMAIN", Value = smtpDomain));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_AUTH_METHOD", Value = smtpAuthMethod));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_OPENSSL_VERIFY_MODE", Value = smtpOpenSslVerifyMode));
        input (TaskDefinitionKeyValuePairArgs(Name = "SMTP_ENABLE_STARTTLS", Value = smtpEnableStarttls));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_ALIAS_HOST", Value = s3AliasHost));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_ENABLED", Value = s3Enabled));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_BUCKET", Value = s3Bucket));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "AWS_ACCESS_KEY_ID", Value = awsAccessKeyId)));
        io (Output.CreateSecret (TaskDefinitionKeyValuePairArgs(Name = "AWS_SECRET_ACCESS_KEY", Value = awsSecretAccessKey)));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_REGION", Value = s3Region));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_PROTOCOL", Value = s3Protocol));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_HOSTNAME", Value = s3Hostname));
        input (TaskDefinitionKeyValuePairArgs(Name = "S3_PERMISSION", Value = ""));
        input (TaskDefinitionKeyValuePairArgs(Name = "SKIP_POST_DEPLOYMENT_MIGRATIONS", Value = skipPostDeploymentMigrations))
    ]

    
