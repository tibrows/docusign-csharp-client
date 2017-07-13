using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Owin.Hosting;
using Owin;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using DocuSign.eSign.Model;
using DocuSign.eSign.Client;

namespace SdkTests
{
    public class Utils
    {
        // This will be returned to the test via the callback url after the
        // user authenticates via the browser.
        public static string AccessCode { get; internal set; }

        // This will be filled in with the access_token retrieved from the token endpoint using the code above.
        // This is the Bearer token that will be used to make API calls.
        public static string AccessToken { get; set; }
        public static string StateValue { get; internal set; }

        // This event handle is used to block the self-hosted Web service in the test
        // until the OAuth login is completed.
        public static ManualResetEvent WaitForCallbackEvent = new ManualResetEvent(false);

        public static string CreateAuthHeader(string userName, string password, string integratorKey)
        {
            DocuSignCredentials dsCreds = new DocuSignCredentials()
            {
                Username = userName,
                Password = password,
                IntegratorKey = integratorKey
            };

            string authHeader = Newtonsoft.Json.JsonConvert.SerializeObject(dsCreds);
            return authHeader;
        }

        internal static EnvelopeTemplate CreateDefaultTemplate(string templateName = null)
        {
            // Read a file from disk to use as a document.

            EnvelopeTemplate templateDef = new EnvelopeTemplate();
            byte[] fileBytes = File.ReadAllBytes(TestConfig.SignTest1File);

            templateDef.EmailSubject = "Template " + DateTime.Now.ToString();
            templateDef.EnvelopeTemplateDefinition = new EnvelopeTemplateDefinition();

            string tempName = templateName;
            if (tempName == null) tempName = "Template Name " + DateTime.Now.ToString();
            templateDef.EnvelopeTemplateDefinition.Name = tempName;

            Document doc = new Document();
            doc.DocumentBase64 = System.Convert.ToBase64String(fileBytes);
            doc.Name = "TestFile.pdf";
            doc.DocumentId = "1";

            templateDef.Documents = new List<Document>();
            templateDef.Documents.Add(doc);

            // Add a recipient to sign the documeent
            Signer signer = new Signer();
            signer.RoleName = "Signer1";
            signer.RecipientId = "1";



            // Create a SignHere tab somewhere on the document for the signer to sign
            signer.Tabs = new Tabs();
            signer.Tabs.SignHereTabs = new List<SignHere>();
            SignHere signHere = new SignHere();
            signHere.DocumentId = "1";
            signHere.PageNumber = "1";
            signHere.RecipientId = "1";
            signHere.XPosition = "200";
            signHere.YPosition = "100";
            //signHere.ScaleValue = "0.5";
            signer.Tabs.SignHereTabs.Add(signHere);

            templateDef.Recipients = new Recipients();
            templateDef.Recipients.Signers = new List<Signer>();
            templateDef.Recipients.Signers.Add(signer);
            return templateDef;
        }

        internal static void ConfigureApiClient()
        {
            ApiClient apiClient = new ApiClient(TestConfig.BaseUrl);
            string authHeader = Utils.CreateAuthHeader(TestConfig.UserName, TestConfig.Password, TestConfig.IntegratorKey);
            // set client in global config so we don't need to pass it to each API object.
            Configuration.Default.ApiClient = apiClient;

            if (Configuration.Default.DefaultHeader.ContainsKey("X-DocuSign-Authentication"))
            {
                Configuration.Default.DefaultHeader.Remove("X-DocuSign-Authentication");
            }
            Configuration.Default.AddDefaultHeader("X-DocuSign-Authentication", authHeader);

        }

        internal static void ConfigureOAuthApiClient()
        {

            // Make an API call with the token
            ApiClient apiClient = new ApiClient(TestConfig.BaseUrl);
            Configuration.Default.ApiClient = apiClient;

            // Initiate the browser session to the Authentication server
            // so the user can login.
            string accountServerAuthUrl = apiClient.GetAuthorizationUri(TestConfig.ClientId, TestConfig.RedirectUrl, true, TestConfig.StateOptional);
            System.Diagnostics.Process.Start(accountServerAuthUrl);

            // Launch a self-hosted web server to accepte the redirect_url call
            // after the user finishes authentication.
            using (WebApp.Start<Startup>("http://localhost:3000"))
            {
                // This waits for the redirect_url to be received in the REST controller
                // (see classes below) and then sleeps a short time to allow the response
                // to be returned to the web browser before the server session ends.
                WaitForCallbackEvent.WaitOne(60000, false);
                Thread.Sleep(1000);
            }

            string accessToken = apiClient.GetOAuthToken(TestConfig.ClientId, TestConfig.ClientSecret, true, AccessCode);
        }

        internal static EnvelopeDefinition CreateDraftEnvelopeDefinition()
        {
            // Read a file from disk to use as a document.
            byte[] fileBytes = File.ReadAllBytes(TestConfig.SignTest1File);

            EnvelopeDefinition envDef = new EnvelopeDefinition();
            envDef.EmailSubject = "Please Sign my C# SDK Envelope";

            Document doc = new Document();
            doc.DocumentBase64 = System.Convert.ToBase64String(fileBytes);
            doc.Name = "TestFile.pdf";
            doc.DocumentId = "1";

            envDef.Documents = new List<Document>();
            envDef.Documents.Add(doc);

            // Add a recipient to sign the documeent
            Signer signer = new Signer();
            signer.Email = TestConfig.UserName;  // use name is same as my email
            signer.Name = "Pat Developer";
            signer.RecipientId = "1";



            // Create a SignHere tab somewhere on the document for the signer to sign
            signer.Tabs = new Tabs();
            signer.Tabs.SignHereTabs = new List<SignHere>();
            SignHere signHere = new SignHere();
            signHere.DocumentId = "1";
            signHere.PageNumber = "1";
            signHere.RecipientId = "1";
            signHere.XPosition = "100";
            signHere.YPosition = "100";
            //signHere.ScaleValue = "0.5";
            signer.Tabs.SignHereTabs.Add(signHere);

            envDef.Recipients = new Recipients();
            envDef.Recipients.Signers = new List<Signer>();
            envDef.Recipients.Signers.Add(signer);
            return envDef;
        }
    }

    // Configuration for self-hosted Web service. THis allows the test to call out to the
    // Account Server endponts and have the resulting browser login session redirect
    // directly into this test.
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configure Web API for self-host. 
            var config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "auth/{controller}",
                defaults: new { controller = "callback", id = RouteParameter.Optional }
            );

            app.UseWebApi(config);
        }
    }

    // API Controller and action called via the redirect_url registered for thie client_id
    public class callbackController : ApiController
    {
        // GET auth/callback 
        public HttpResponseMessage Get()
        {
            Utils.AccessCode = Request.RequestUri.ParseQueryString()["code"];

            // state is app-specific string that may be passed around for validation.
            Utils.StateValue = Request.RequestUri.ParseQueryString()["state"];

            HttpResponseMessage response = new HttpResponseMessage();
            response.Content = new StringContent("Redirect Completed");
            response.StatusCode = HttpStatusCode.OK;

            // Signal the main test that the response has been received.
            Utils.WaitForCallbackEvent.Set();
            return response;
        }
    }

}
