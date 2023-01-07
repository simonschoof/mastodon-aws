namespace MastodonAwsServices.Config 

module Secrets = 
    open Amazon.SecretsManager
    open Amazon.SecretsManager.Model
    open System.Threading


    let resolveSecret (secretName: string) =  
        let awsSecretManagerClient = new AmazonSecretsManagerClient()
        let mutable secretValueRequest = GetSecretValueRequest()
        secretValueRequest.SecretId <- secretName

    
        let asyncSecrets = async {
            let! result = awsSecretManagerClient.GetSecretValueAsync(secretValueRequest, CancellationToken(false))
                        |> Async.AwaitTask  
            return result
        }
        let secretResolved = Async.RunSynchronously(asyncSecrets)
        secretResolved.SecretString
