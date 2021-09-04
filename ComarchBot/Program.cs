using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ComarchBot
{
    class Program
    {
        static HttpClient client;

        static async Task Main(string[] args)
        {
            
            var baseAddress = new Uri("https://ecodweb.comarch.ru");
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
            client = new HttpClient(handler) { BaseAddress = baseAddress};
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/92.0.4515.159 Safari/537.36");
            
            // 1. First request to get aspx <input> values
            string urlLogin = "/App/Pages/Login.aspx?info=sessionExpired&ReturnUrl=%2fApp%2fPages%2fOutbox.aspx";
            var response = await client.GetAsync(urlLogin);
            var responseString = await response.Content.ReadAsStringAsync();

            // 2. Login request
            string login;
            using (var f = File.OpenText("_Login.txt")) {login = f.ReadLine();}
            string password;
            using (var f = File.OpenText("_Password.txt")) {password = f.ReadLine(); }

            var values = new Dictionary<string, string>
            {
                { "__VIEWSTATE", GetInputValue("__VIEWSTATE", responseString)},
                { "__VIEWSTATEGENERATOR", GetInputValue("__VIEWSTATEGENERATOR", responseString)},
                { "__EVENTVALIDATION", GetInputValue("__EVENTVALIDATION", responseString)},
                { "MainLogin$UserName", login },
                { "MainLogin$Password", password },
                { "MainLogin$CtrlLoginButton", "Вход" }
            };
            var content = new FormUrlEncodedContent(values);
            response = await client.PostAsync(urlLogin, content);
            response.EnsureSuccessStatusCode();

            // 3. Error outbox request 
            string outboxFilters = "SrcPart_Name=0&DestPart_Name=0&BusinessType_Type=0&Relation=0&DocumentNumber=&DateFrom=&DateTo=&DateDocumentFrom=&DateDocumentTo=&PrintedDocuments=Blank&CanceledDocuments=Blank&FinalizedDocuments=Blank&Error=Checked&DeliveryPointIln=&MessageType=&DocumentReferenceNumber=";
            cookieContainer.Add(baseAddress, new Cookie("OutboxFilterParam", outboxFilters));
            cookieContainer.Add(baseAddress, new Cookie("OutboxPageSize", "50"));
            var urlOutbox = "/App/Pages/Outbox.aspx";
            response = await client.GetAsync(urlOutbox);
            response.EnsureSuccessStatusCode();
            responseString = await response.Content.ReadAsStringAsync();            

            // Parse Error Docs Response
            var errDocPattern = "<div id=\".*?DocumentNumber\".*?>(.*?)<\\/div>.*?<div id=\".*?WebIntOut_TimeStamp\" .*?>(.*?)<\\/div>";
            Regex regex = new Regex(errDocPattern);
            var matches = regex.Matches(responseString);
            foreach (Match match in matches)
            {
                var docNo = match.Groups[1].Value;
                var processDate = match.Groups[2].Value;
                Console.WriteLine($"{docNo} {processDate}");
            }
            
            Console.ReadLine();

        }        

        private static string GetInputValue(string inputName, string responseBody)
        {
            Regex regex = new Regex($"<input type=\"hidden\" name=\"{inputName}\".*?value=\"(.*?)\"");
            return regex.Match(responseBody).Groups[1].Value;
        }
    }
}
