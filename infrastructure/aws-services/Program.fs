module Program

open MastodonAwsServices.ElastiCache
open MastodonAwsServices.Rds
open MastodonAwsServices.S3
open MastodonAwsServices.Ecs
open Pulumi.FSharp

let infra () =

  let outputs = 
    createBucket() 
    @ createRdsCluster()
    // @ createElastiCacheCluster()
    @ createEcs()
  
  dict outputs

[<EntryPoint>]
let main _ =
  Deployment.run infra
