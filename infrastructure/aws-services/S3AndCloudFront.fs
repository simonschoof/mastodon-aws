namespace MastodonAwsServices


module S3AndCloudFront =

    open Pulumi
    open Pulumi.Aws
    open Pulumi.Aws.CloudFront
    open Pulumi.Aws.CloudFront.Inputs
    open Pulumi.Aws.Iam
    open Pulumi.Aws.Iam.Inputs
    open Pulumi.Aws.S3
    open Pulumi.FSharp
    open MastodonAwsServices.Config.Values

    let createBucketAndDistribution () = // Create an AWS resource (S3 Bucket)
        (*
-----------------------
S3 
-----------------------
*)
        let bucket =
            let bucketName = prefixMastodonResource "s3-storage"

            let bucketArgs = BucketArgs(Acl = "private")

            Bucket(bucketName, bucketArgs)

        let bucketPublicAccessBlock =
            let bucketPublicAccessBlockArgs = 
                BucketPublicAccessBlockArgs(
                    Bucket = bucket.Id,
                    BlockPublicAcls = true,
                    BlockPublicPolicy = true,
                    IgnorePublicAcls = true,
                    RestrictPublicBuckets = true
                )

            BucketPublicAccessBlock(prefixMastodonResource "s3-storage-public-access-block", bucketPublicAccessBlockArgs)
        
        (*
-----------------------
S3 access for Mastodon 
-----------------------
*)
        //
        // Needed S3 permissions for Mastodon to be able to access th S3 bucket
        // https://gist.github.com/ftpmorph/299c00907c827fbca883eeb45e6a7dc4?permalink_comment_id=4374053
        //
        let policyAttachment =
            let limitedPermissionsToOneBucketStatement =
                GetPolicyDocumentStatementInputArgs(
                    Effect = "Allow",
                    Actions =
                        inputList [ input "s3:ListBucket"
                                    input "s3:GetBucketLocation" ],
                    Resources = inputList [ io bucket.Arn ]
                )

            let permissionsToBucketStatement =
                GetPolicyDocumentStatementInputArgs(
                    Effect = "Allow",
                    Actions =
                        inputList [ input "s3:GetObject"
                                    input "s3:GetObjectAcl"
                                    input "s3:PutObject"
                                    input "s3:PutObjectAcl"
                                    input "s3:DeleteObject"
                                    input "s3:AbortMultipartUpload"
                                    input "s3:ListMultipartUploadParts" ],
                    Resources = inputList [ io (Output.Format($"{bucket.Arn}/*")) ]
                )

            let policyDocumentInvokeArgs =
                GetPolicyDocumentInvokeArgs(
                    Statements =
                        inputList [ input limitedPermissionsToOneBucketStatement
                                    input permissionsToBucketStatement ]
                )

            let policyDocument =
                GetPolicyDocument.Invoke(policyDocumentInvokeArgs)

            let policyArgs = PolicyArgs(PolicyDocument = io (policyDocument.Apply(fun (pd) -> pd.Json)))

            let policy = Policy(prefixMastodonResource "s3-access-policiy", policyArgs)

            let group = Group(prefixMastodonResource "s3-access-group")

            let policyAttachmentArgs = PolicyAttachmentArgs(Groups = group.Name, PolicyArn = policy.Arn)

            PolicyAttachment(prefixMastodonResource "access-group-policiy-attachment", policyAttachmentArgs)

        (*
-----------------------
Cloudfront as S3 alias 
-----------------------
*)      
        let originAccessIdentity =

            let originAccessIdentityArgs =
                OriginAccessIdentityArgs(Comment = "Access identy to access the origin bucket")

            OriginAccessIdentity("Cloudfront Origin Access Identity", originAccessIdentityArgs)

        let cloudFrontPrincipal =
            GetPolicyDocumentStatementPrincipalInputArgs(
                Type = "AWS",
                Identifiers = inputList [ io originAccessIdentity.IamArn ]
            )

        let imageBucketPolicy =

            let getObjectStatement =
                GetPolicyDocumentStatementInputArgs(
                    Principals = inputList [ input cloudFrontPrincipal ],
                    Actions = inputList [ input "s3:GetObject" ],
                    Resources =
                        inputList [ io bucket.Arn
                                    io (Output.Format($"{bucket.Arn}/*")) ]
                )

            let policyDocumentInvokeArgs =
                GetPolicyDocumentInvokeArgs(
                    Statements =
                        inputList [ input getObjectStatement ]
                )

            let policyDocument =
                GetPolicyDocument.Invoke(policyDocumentInvokeArgs)

            let bucketPolicyArgs =
                BucketPolicyArgs(Bucket = bucket.Id, Policy = io (policyDocument.Apply(fun (pd) -> pd.Json)))

            BucketPolicy(prefixMastodonResource "image-bucket-policy", bucketPolicyArgs)

        let certInvokeOptions =
            let invokeOptions = InvokeOptions()
            invokeOptions.Provider <- Provider("useast1", ProviderArgs(Region = "us-east-1"))
            invokeOptions

        let getCertificateInvokeArgs =
            Pulumi.Aws.Acm.GetCertificateInvokeArgs(
                Domain = "mastodonmedia.simonschoof.com",
                MostRecent = true,
                Types = inputList [ input "AMAZON_ISSUED" ]
            )

        let cert =
            Pulumi.Aws.Acm.GetCertificate.Invoke(getCertificateInvokeArgs, certInvokeOptions)
        
        let cloudFrontDistribution = 
            let s3OriginConfigArgs = DistributionOriginS3OriginConfigArgs(OriginAccessIdentity = originAccessIdentity.CloudfrontAccessIdentityPath)

            let originArgs =
                DistributionOriginArgs(
                    DomainName = bucket.BucketRegionalDomainName,
                    OriginId = "myS3Origin",
                    S3OriginConfig = s3OriginConfigArgs
                )

            let viewerCertificate =
                DistributionViewerCertificateArgs(AcmCertificateArn = io (cert.Apply(fun cert -> cert.Arn)), SslSupportMethod = "sni-only")

            let forwardeValueCookies =
                DistributionDefaultCacheBehaviorForwardedValuesCookiesArgs(Forward = "none")
            
            let forwardedValuesArgs =
                DistributionDefaultCacheBehaviorForwardedValuesArgs(
                    QueryString = true,
                    Cookies = forwardeValueCookies
                )

            let defaultCacheBehaviorArgs =
                DistributionDefaultCacheBehaviorArgs(
                    AllowedMethods =
                        inputList [ input "GET"
                                    input "HEAD"
                                    input "OPTIONS" ],
                    CachedMethods = inputList [ input "GET"; input "HEAD" ],
                    TargetOriginId = "myS3Origin",
                    ForwardedValues = forwardedValuesArgs,
                    ViewerProtocolPolicy = "redirect-to-https",
                    MinTtl = 100,
                    DefaultTtl = 3600,
                    MaxTtl = 86400,
                    SmoothStreaming = false,
                    Compress = true
                )

            let geoRestrictions =
                DistributionRestrictionsGeoRestrictionArgs(RestrictionType = "none")

            let restrictionArgs =
                DistributionRestrictionsArgs(GeoRestriction = geoRestrictions)

            let cloudFrontDistributionArgs =
                DistributionArgs(
                    Origins = originArgs,
                    Enabled = true,
                    Aliases = inputList [input "mastodonmedia.simonschoof.com"],
                    Comment = "Distribution as S3 alias for Mastodon content delivery",
                    DefaultRootObject = "index.html",
                    PriceClass = "PriceClass_100",
                    ViewerCertificate = viewerCertificate,
                    DefaultCacheBehavior = defaultCacheBehaviorArgs,
                    Restrictions = restrictionArgs
                )

            Distribution(prefixMastodonResource "media-distribution", cloudFrontDistributionArgs)

        // Export the name of the bucket
        [ ("bucketName", bucket.Id :> obj) ]
