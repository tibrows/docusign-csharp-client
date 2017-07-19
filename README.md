# DocuSign C# SDK

The DocuSign C# SDK is a Swagger-based auo-generated library that can be used to quickly interact with the DocuSign REST API.  It provides the C# versions of REST request and response models and endpoints. This project contains the core source code for the SDK along with tests and a few examples showing its use.  

**ORGANIZATION**

  * `\sdk`:  Source code for the entire SDK 
  * `\test`:  Unit tests and sample code
  
**NOTE**
  * The tests for Account Server OAuth2 Authentication are early concept and not for external use. This is pre-release software.

**HOW TO USE**

To reference the SDK, add the following References to your Visual Studio project from SDKs\csharp\sdk\bin:

```
DocuSign.Core.dll
NewtonsoftJson.dll
RestSharp.dll
```
  
In the C# source code, add the following usings:

```
using DocuSign.Core.Api;  
using DocuSign.Core.Client;
using DocuSign.Core.Model;
```

**EXAMPLES**

For an example of how to make the Login API call then subsequently a Signature Request see (sdks\csharp\test\sdktests\sdktests\sdktests1.cs).
