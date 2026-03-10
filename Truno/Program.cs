// Program.cs
// dotnet new console -n TruCommerceOrderConsole
// dotnet run

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

// Program.cs
// dotnet new console -n TruCommerceOrderConsole
// dotnet run
// or: dotnet run -- path/to/order.json

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

internal static class Program
{
    private const string BaseUrl = "https://test.trucommerce.com/v2";
    private const string EndpointPath = "/orderService";

    private const string ApiUser = "epic-test-ecom";
    private const string ApiPassword = "3M4R#mUEHVYbG8ZV";

    private static async Task<int> Main(string[] args)
    {
        try
        {
            var json = args.Length > 0
                ? await System.IO.File.ReadAllTextAsync(args[0])
                : GetSampleOrderJson();

            var responseBody = await PostOrderAsync(json);

            Console.WriteLine("Response:");
            Console.WriteLine(responseBody);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error:");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task<string> PostOrderAsync(string postJson)
    {
        var fullUrl = BaseUrl.TrimEnd('/') + EndpointPath;

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        var basicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ApiUser}:{ApiPassword}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Epic-Test");
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var content = new StringContent(postJson, Encoding.UTF8, "application/json");

        Console.WriteLine($"POST {fullUrl}");
        using var resp = await http.PostAsync(fullUrl, content);
        var body = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");
        // resp.EnsureSuccessStatusCode(); // uncomment if you want to fail on non-2xx

        return body;
    }

    private static string GetSampleOrderJson() => """
{  
    "status": "1",  
    "type": "1",  
    "processorReceived":"2026-03-09 15:00:25",
    "processorOrderNumber": "900900123",  
    "posLoyaltyNumber": "","orderEntryPOSs": [  
        {  
        "itemSequence": 1,  
            "upc": "00001200000017",  
            "originalUpc": "00001200000017",  
            "department": "2",  
            "price": 15.49,  
        "amount": 15.49,  
            "quantity": 1.0,  
            "weight": 0  
        }  
    ],  
    "orderEntryTenders": [  
     {  
        "posTenderType": "",  
        "posTenderAmount":0.00  
     }  
    ],  
   "orderEntryTaxes": [  
  {  
    "taxCode": "A",  
    "taxableAmount": 15.49, 
     "taxAmount": 1.00 
   }
   ],  
    "orderTotals": {  
  "total": 15.49,  
  "subTotal": 15.49,  
  "taxAmount": 1.00,  
  "amountTendered": 0.00,  
  "foodstampTendered": "",  
  "balanceDue": "",  
  "foodstampTotal": "",  
  "foodstampBalanceDue": "",  
  "couponTotal": "",  
  "coupons": "",  
  "items": 1  
    }  
} 
""";
}