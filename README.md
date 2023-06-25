# image-generation-server
A micro service to generate AI image for English vocabulary learning application (www.funfunspell.com)

## Dependencies

1. ASP.NET core
2. Google Firebase
3. [Replicate AI](https://replicate.com/home)
4. Kubernetes
5. Docker

## Environment Variables
Required
```shell
ReplicateAiServiceOptions__Token=<API Token to access Replicate.com>

GoogleApplicationCredentials=<JSON file to access google cloud>

ApiKeyMiddlewareOptions__ApiKeys=<API Keys for client access this service>
```