using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;



string OWMKey = NWO_OWMservice.Properties.Resources.OWMKEY;

DateTime startTime = DateTime.MinValue;

wather[,] wathers = new wather[20,10];


startTime = DateTime.UtcNow;
for (int i = 0; i < 20; i++)
    for (int j = 0; j < 10; j++)
    {
        wathers[i, j] = GetOWMData(i, j);
        Console.Clear();
        Console.WriteLine((i * 10 + j + 1) + "/200");
    }


new Thread(() =>
{
    AysncEchoServer().Wait();
}).Start();



Thread updateThread = new(() =>
{
    while (true)
    {
        TimeSpan Ltime = (DateTime.UtcNow - startTime);


        if (Ltime.TotalHours > 3)
        {
            startTime = DateTime.UtcNow;
            Ltime = (DateTime.UtcNow - startTime);
            for(int i=0;i<20;i++)
                for (int j = 0; j < 10; j++)
                {
                    wathers[i,j]=GetOWMData(i,j);
                    Console.WriteLine((i*10+j+1)+"/200");
                    Thread.Sleep(500);
                }
            
        }
        Console.WriteLine(Ltime.ToString());
        Thread.Sleep(10000);
    }
    System.GC.Collect();
});

updateThread.IsBackground = true;
updateThread.Start();



async Task AysncEchoServer()
{
    TcpListener listener = new TcpListener(IPAddress.Any, 29000);
    listener.Start();
    while (true)
    {
        TcpClient client = await listener.AcceptTcpClientAsync();
        Task.Factory.StartNew(AsyncTcpProcess, client);
    }
}

async void AsyncTcpProcess(object o)
{
    System.Globalization.CultureInfo culInfo = new System.Globalization.CultureInfo("en-US");

    TcpClient client = (TcpClient)o;
    NetworkStream ns = client.GetStream();
    byte[] buffer = new byte[1024];
    int bytesRead = await ns.ReadAsync(buffer, 0, buffer.Length);

    float lon=0;
    float lat=0;

    StringBuilder sb = new StringBuilder(100);

    sb.AppendLine("HTTP/1.1 200 ok");
    sb.AppendLine("content-type:text/html; charset=UTF-8");
    sb.AppendLine();

    string Data = Encoding.ASCII.GetString(buffer, 0, bytesRead);

    int lonStart = Data.IndexOf("GET /?");
    if (lonStart > -1)
    {
        Data = Data.Substring(lonStart + 5);
        Data = Data.Substring(0, Data.IndexOf("HTTP"));


        string[] val = Data.Split(new char[] { '?', '&' });

        if (val.Length > 1)
        {
            if (float.TryParse(val[1].Split('=')[1], System.Globalization.NumberStyles.Any, culInfo, out lon))
            {
                if (float.TryParse(val[2].Split('=')[1], System.Globalization.NumberStyles.Any, culInfo, out lat))
                {
                    wather w = GetPointWather(lon, lat);
                    sb.Append("{\"temp\":");
                    sb.Append(w.main.temp.ToString());
                    sb.Append(",");
                    sb.Append("\"wind\":{");
                    sb.Append("\"speed\":");
                    sb.Append(w.wind.speed.ToString());
                    sb.Append(',');
                    sb.Append("\"deg\":");
                    sb.Append(w.wind.deg.ToString());
                    sb.Append("},");
                    sb.Append("\"clouds\":");
                    sb.Append(w.clouds.all.ToString());
                    sb.Append("}");
                }
            }
        }
    }
    else
    {
        sb.Append("Weater Service is working");
        sb.Append("<br>");
        sb.Append(DateTime.Now.ToString());
    }

    byte[] response = Encoding.ASCII.GetBytes(sb.ToString());
    await ns.WriteAsync(response, 0, response.Length);    

    ns.Close();
    client.Close();
}


wather GetPointWather(float x,float y)
{
    x = Math.Min( ((x%360 + 180) / 36) ,19 );
    y = Math.Min( ((y + 90) / 18) ,9 );

    wather Data = new wather();

    Data.main = new wather.Temp();
    Data.clouds= new wather.Clouds();


    Data.main.temp =
        Lerp(
            Lerp(wathers[(int)x, (int)y].main.temp, wathers[Math.Min((int)x + 1, 19), (int)y].main.temp, x % 1),
            Lerp(wathers[(int)x, Math.Min((int)y+1, 9)].main.temp, wathers[Math.Min((int)x + 1, 19), Math.Min((int)y+1, 9)].main.temp, x % 1), y % 1);

    Data.wind = new wather.Wind();

    Data.wind.speed =
        Lerp(
            Lerp(wathers[(int)x, (int)y].wind.speed, wathers[ Math.Min((int)x + 1,19 ) , (int)y].wind.speed, x % 1),
            Lerp(wathers[(int)x, Math.Min((int)y+1, 9) ].wind.speed, wathers[ Math.Min((int)x + 1, 19) , Math.Min((int)y+1, 9) ].wind.speed, x % 1),y % 1);
    
    Data.wind.deg =
        Lerp(
            Lerp(wathers[(int)x, (int)y].wind.deg, wathers[Math.Min((int)x + 1, 19), (int)y].wind.deg, x % 1),
            Lerp(wathers[(int)x, Math.Min((int)y + 1, 9)].wind.deg, wathers[Math.Min((int)x + 1, 19), Math.Min((int)y + 1, 9)].wind.deg, x % 1), y % 1);


    Data.clouds.all =
    Lerp(
        Lerp(wathers[(int)x, (int)y].clouds.all, wathers[Math.Min((int)x + 1, 19), (int)y].clouds.all, x % 1),
        Lerp(wathers[(int)x, Math.Min((int)y + 1, 9)].clouds.all, wathers[Math.Min((int)x + 1, 19), Math.Min((int)y + 1, 9)].clouds.all, x % 1), y % 1);

    return Data;
}


wather GetOWMData(int x,int y)
{

    string jsonString = Request_Json("https://api.openweathermap.org/data/2.5/weather?lat=" + (20 * y - 90) + "&lon=" + (18 * x - 180) + "&appid=" + OWMKey);

    if (jsonString==string.Empty)
    {
        jsonString = Request_Json("https://api.openweathermap.org/data/2.5/weather?lat=" + (20 * y - 90) + "&lon=" + (18 * x - 180) + "&appid=" + OWMKey);
    }
    wather pObj = JsonConvert.DeserializeObject<wather>(jsonString);


    return pObj;
}

static float Lerp(float firstFloat, float secondFloat, float by)
{
    return firstFloat * (1 - by) + secondFloat * by;
}

string Request_Json(string url)
{
    string result = string.Empty;
    try
    {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        using (var response = (HttpWebResponse)request.GetResponse())
        {
            using (Stream responseStream = response.GetResponseStream())
            {
                using (StreamReader stream = new StreamReader(responseStream, Encoding.UTF8))
                {
                    result = stream.ReadToEnd();
                }
            }
        }
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
    }
    return result;
}


public class wather
{

    public Temp main { get; set; }
    public Wind wind { get; set; }
    public Clouds clouds { get; set; }

    public class Temp
    {
        public float temp { get; set; }
    }

    public class Wind
    {
        public float speed { get; set; }
        public float deg { get; set; }
    }
    
    public class Clouds
    {
        public float all { get; set; }
    }
}