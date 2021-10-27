using Microsoft.Extensions.DependencyInjection;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;



var urlStatistics = new List<UrlStatistic>();
Console.WriteLine("Web Statistics");
Console.WriteLine("==============");

//HttpClient in Console Application
var serviceProvider = new ServiceCollection().AddHttpClient().BuildServiceProvider();
var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
var client = httpClientFactory.CreateClient();

//Webadressen aus CSV-Datei laden
string[] siteUrls = await GetUrlsFromCsvAsync("sites.csv");
Stopwatch stopWatch = new();


// ****************** Urls parallel aus dem Web laden und Statistik erstellen **************************
Console.WriteLine();
Console.WriteLine("========== Http-Get with parallel Tasks ==========");
stopWatch.Start();
List<Task<UrlStatistic>> getTasks = new();
for (int i = 0; i < siteUrls.Length; i++)
{
    int j = i;
    var url = siteUrls[j];
    var task = Task.Run(() => GetStatisticsForUrlByTask(j, client, url));
    getTasks.Add(task);
}
for (int i = 0; i < siteUrls.Length; i++)
{
    var task = getTasks[i];
    var statistic = task.Result;
    Console.WriteLine($"{statistic.Index,3} {statistic.Url,-20} Size: {statistic.Size,10} GetTime: {statistic.GetMilliSeconds,5}ms ThreadId: {statistic.ThreadId,2} Threads: {statistic.ThreadsInPool,3}");
    urlStatistics.Add(statistic);
}
stopWatch.Stop();
Console.WriteLine();
Console.WriteLine("Auswertung für parallele Tasks:");
Console.WriteLine("==============================");
Console.WriteLine($"Gesamtzeit: {stopWatch.ElapsedMilliseconds}ms, längste Ladedauer: {urlStatistics.Max(s => s.GetMilliSeconds)}");
Console.WriteLine($"{urlStatistics.GroupBy(s => s.ThreadId).Count()} Threads für {urlStatistics.Count} Get-Tasks");
//Console.Write("Beenden mit Eingabetaste ...");
//Console.ReadLine();
Console.WriteLine();
// **************************************************************************************************************

// ****************** Urls mit async/await aus dem Web laden und Statistik erstellen **************************
Console.WriteLine();
Console.WriteLine("========== Http-Get with async/await  ==========");
stopWatch.Reset();
stopWatch.Start();
for (int i = 0; i < siteUrls.Length; i++)
{
    var url = siteUrls[i];
    urlStatistics[i] = await GetStatisticsForUrlAsync(i, client, url);
}
Console.WriteLine();
for (int i = 0; i < siteUrls.Length; i++)
{
    Console.WriteLine($"{urlStatistics[i].Index,3} {urlStatistics[i].Url,-20} Size: {urlStatistics[i].Size,10} GetTime: {urlStatistics[i].GetMilliSeconds,5}ms ThreadId: {urlStatistics[i].ThreadId,2} Threads: {urlStatistics[i].ThreadsInPool,3}");
}
//Console.WriteLine("========== Get with async/await: all Tasks started ==========");
//for (int i = 0; i < siteUrls.Length; i++)
//{
//    var statistic = urlStatistics[i];
//    urlStatistics.Add(statistic);
//}
stopWatch.Stop();
Console.WriteLine();
Console.WriteLine("Auswertung für async/await Tasks:");
Console.WriteLine("================================");
Console.WriteLine($"Gesamtzeit: {stopWatch.ElapsedMilliseconds}ms, längste Ladedauer: {urlStatistics.Max(s => s.GetMilliSeconds)}");
Console.WriteLine($"{urlStatistics.GroupBy(s => s.ThreadId).Count()} Threads für {urlStatistics.Count} Get-Tasks");
Console.Write("Beenden mit Eingabetaste ...");
Console.ReadLine();
// **************************************************************************************************************



static Task<UrlStatistic> GetStatisticsForUrlByTask(int index, HttpClient client, string url)
{
    Stopwatch stopWatch = new();
    stopWatch.Start();
    var httpRequest = Task.Run(() =>client.GetAsync("http://"+url));
    var urlTask = httpRequest.ContinueWith((hr)=> {
        stopWatch.Stop();
        return new UrlStatistic(index+1, url, stopWatch.ElapsedMilliseconds, Convert.ToInt32(hr.Result.Content.Headers.ContentLength), Thread.CurrentThread.ManagedThreadId, ThreadPool.ThreadCount);
    });
    return urlTask;
}

static async Task<UrlStatistic> GetStatisticsForUrlAsync(int index, HttpClient client, string url)
{
    Stopwatch stopWatch = new();
    stopWatch.Start();
    var httpResponse = await client.GetAsync("http://"+url);
    stopWatch.Stop();
    return new UrlStatistic(index+1, url, stopWatch.ElapsedMilliseconds, Convert.ToInt32(httpResponse.Content.Headers.ContentLength), Thread.CurrentThread.ManagedThreadId, ThreadPool.ThreadCount);
}

/// <summary>
/// Lade die Urls aus der CSV-Datei
/// </summary>
/// <param name="fileName"></param>
/// <returns></returns>
static async Task<string[]> GetUrlsFromCsvAsync(string fileName)
{
    var lines = await File.ReadAllLinesAsync(fileName);
    var urls = lines
                   .Skip(1)
                   .Select(line => line.Split(';')[1])
                   .ToArray();
    return urls;
}

record UrlStatistic(int Index, string Url, long GetMilliSeconds, int Size, int ThreadId, int ThreadsInPool);
