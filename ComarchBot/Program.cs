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
        static async Task Main(string[] args)
        {
            System.Net.ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            var baseAddress = new Uri("https://ecodweb.comarch.ru");
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
            var client = new HttpClient(handler) { BaseAddress = baseAddress};
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.61 Safari/537.36");
            
            // 1. First request to get aspx <input> values
            var urlLogin = "/App/Pages/Login.aspx";
            var response = await client.GetAsync(urlLogin);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();

            // 2. Login request
            var values = new Dictionary<string, string>
            {
                { "__VIEWSTATE", GetInputValue("__VIEWSTATE", responseString)},              
                { "__EVENTVALIDATION", GetInputValue("__EVENTVALIDATION", responseString)},
                { "MainLogin$UserName", ReadLineFromFile("_Login.txt") },
                { "MainLogin$Password", ReadLineFromFile("_Password.txt") },
                { "MainLogin$CtrlLoginButton", "Вход" }
            };
            var content = new FormUrlEncodedContent(values);
            response = await client.PostAsync(urlLogin, content);
            response.EnsureSuccessStatusCode();

            // 3. Error outbox request 
            cookieContainer.Add(baseAddress, new Cookie("OutboxFilterParam", "Error=Checked"));
            cookieContainer.Add(baseAddress, new Cookie("OutboxPageSize", "50"));
            response = await client.GetAsync("/App/Pages/Outbox.aspx");
            response.EnsureSuccessStatusCode();
            responseString = await response.Content.ReadAsStringAsync();            

            // 4. Parse Error Docs Response
            var errDocPattern = "<div id=\".*?DocumentNumber\".*?>(.*?)<\\/div>.*?<div id=\".*?WebIntOut_TimeStamp\" .*?>(.*?)<\\/div>";
            var regex = new Regex(errDocPattern);
            foreach (Match match in regex.Matches(responseString))
            {
                Console.WriteLine($"docNo: {match.Groups[1].Value}, processDate: {match.Groups[2].Value}");
            }
            
            Console.ReadLine();
        }

        private static string ReadLineFromFile(string fileName)
        {
            using (var f = File.OpenText(fileName)) 
            {
                return f.ReadLine(); 
            }
        }

        private static string GetInputValue(string inputName, string responseBody)
        {
            var regex = new Regex($"<input type=\"hidden\" name=\"{inputName}\".*?value=\"(.*?)\"");
            return regex.Match(responseBody).Groups[1].Value;
        }
    }
}
