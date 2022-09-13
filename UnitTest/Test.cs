using Jack.RemoteLog;
using Jack.RemoteLog.WebApi;
using Jack.RemoteLog.WebApi.Applications;
using Jack.RemoteLog.WebApi.AutoMissions;
using Lucene.Net.Analysis.PanGu;
using Lucene.Net.Analysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System;
using System.Diagnostics;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using static Microsoft.Extensions.Logging.EventSource.LoggingEventSource;
using System.Text;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Jack.RemoteLog.WebApi.Dtos;
using Lucene.Net.QueryParsers.Classic;

namespace UnitTest
{
    [TestClass]
    public class Test
    {
        ServiceCollection _services;
        ServiceProvider _serviceProvider;
        public Test()
        {
            ConfigurationBuilder builder = new ConfigurationBuilder();
            builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            _services = new ServiceCollection();
            _services.AddSingleton<LogChannelRoute>();
            _services.AddSingleton<LogService>();
            _services.AddSingleton<IConfiguration>(Global.Configuration = builder.Build());

            _services.AddLogging(builder =>
            {
                builder.UseJackRemoteLogger(Global.Configuration, "MyApplicationContext");
            });
            _services.AddScoped<TestObject>();

            Global.ServiceProvider = _serviceProvider = _services.BuildServiceProvider();
           
        }

        [TestMethod]
        public void TestPanGu()
        {
            //���������ݴ洢�ĵط��������ǣ�bin\Debug�µ�index�ļ���
            DirectoryInfo INDEX_DIR = new DirectoryInfo(AppContext.BaseDirectory + "index");
            Analyzer analyzer = new PanGuAnalyzer(); //MMSegAnalyzer //StandardAnalyzer

            //����ı�����
            var context = "Exchange:Exchange.BlockScan �յ�BlockScan��Ϣ��{\"BlockNumber\":15524540,\"Txid\":\"0x760fe330799272ce557dadd0def7d5e1a01c079ee1a97629a9160f0ff0201299\",\"Amount\":496.0,\"Time\":\"2022-09-13T02:58:48+00:00\",\"Confirmations\":1,\"Valid\":true,\"PropertyId\":\"0xdac17f958d2ee523a2206206994597c13d831ec7\",\"Nonce\":0,\"CoinType\":102,\"GasPrice\":0.000000014710919597,\"Fee\":0.003047043354287416,\"SenderAddress\":\"0x28c6c06298d514db089934071355e5743bf21d60\",\"ReceivedAddress\":\"0xd6530793f846092226cca60a86d630e0f927432e\",\"Coin\":\"USDT\"}";


            IndexWriter iw;

            var options = new IndexWriterConfig(Lucene.Net.Util.LuceneVersion.LUCENE_48, null);
            options.OpenMode = OpenMode.CREATE_OR_APPEND;

            iw = new IndexWriter(FSDirectory.Open(INDEX_DIR), options);

            //var rangeQuery_2_1 = NumericRangeQuery.NewInt64Range("date", null, DateTimeOffset.Now.ToUnixTimeMilliseconds()*2, true, true);
            //iw.DeleteDocuments(rangeQuery_2_1);
            //iw.Commit();
            //iw.Flush(true,true);

            Task.Run(() => {
                Thread.Sleep(2000);
                while (true)
                {
                    TestSearchPanGu();
                    Debug.WriteLine("������һ��");
                    Thread.Sleep(0);
                }
            });
            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(1000);
                    iw.Commit();
                    iw.Flush(true, false);
                }
            });

            for (int i = 0; i < 100000000;i++)
            {
                //�洢�ĵ��������
                Document doc = new Document();
                //ָ���Ϸ����������ݴ洢��key�Լ�����ģʽ
                doc.Add(new TextField("body", context, Field.Store.YES));

                doc.AddInt32Field("id", i , Field.Store.YES);
              

                var timestamp = DateTimeOffset.Parse(DateTime.Parse("2022-10-1").AddMinutes(i).ToString("yyyy-MM-dd HH:mm:ss")).ToUnixTimeMilliseconds();
                doc.AddInt64Field("date", timestamp, Field.Store.YES);
                //��������ɵ����ݴ洢
                iw.AddDocument(doc , analyzer);
                iw.Commit();
                iw.Flush(true, false);

            }
            iw.Dispose();
            Thread.Sleep(10000000);
        }

        private BooleanQuery AnalyzerKeyword(string keyword, string field)
        {
            BooleanQuery ret = new BooleanQuery();
            var arr = keyword.Split(' ');
            foreach (var str in arr)
            {
                if (string.IsNullOrEmpty(str))
                    continue;

                StringBuilder queryStringBuilder = new StringBuilder();
                string[] words = LuceneAnalyze.AnalyzerKey(str);

                BooleanQuery queryMust = new BooleanQuery();

                foreach (var word in words)
                {
                    if (word.EndsWith("*"))
                    {
                        var newWord = word.Substring(0, word.Length - 1);
                        while (newWord.EndsWith("*"))
                        {
                            newWord = newWord.Substring(0, newWord.Length - 1);
                        }
                        var termQuery = new PrefixQuery(new Term(field, newWord));
                        queryMust.Add(termQuery, Occur.MUST);
                    }
                    else
                    {
                        var termQuery = new TermQuery(new Term(field, word));
                        queryMust.Add(termQuery, Occur.MUST);
                    }
                }
                ret.Add(queryMust, Occur.SHOULD);
            }

            return ret;
        }

        [TestMethod]
        public void TestSearchPanGu()
        {
            var starttime = DateTimeOffset.Parse("2021-10-2").ToUnixTimeMilliseconds();
            var endtime = DateTimeOffset.Parse("2023-10-3").ToUnixTimeMilliseconds();

            var keyword = "�յ�*";

            DirectoryInfo INDEX_DIR = new DirectoryInfo(AppContext.BaseDirectory + "index");
            Analyzer analyzer = new PanGuAnalyzer(); //MMSegAnalyzer //StandardAnalyzer

            using (var indexer = DirectoryReader.Open(FSDirectory.Open(INDEX_DIR)))
            {
                IndexSearcher searcher = new IndexSearcher(indexer);
                //����Ҫ������Key
                QueryParser qp = new QueryParser(Lucene.Net.Util.LuceneVersion.LUCENE_48, "Content", analyzer);
                Query query = AnalyzerKeyword(keyword , "body");

                var rangeQuery_2_1 = NumericRangeQuery.NewInt64Range("date", starttime, endtime, true, true);
                BooleanQuery booleanClauses = new BooleanQuery();
                booleanClauses.Add(query, Occur.MUST);
                //booleanClauses.Add(rangeQuery_2_1, Occur.MUST);


                //��ѯ�ִ����ݣ��Լ����÷��ؼ������������
                TopDocs tds = searcher.Search(booleanClauses, 100);
                Debug.WriteLine("���������: " + tds.TotalHits);
                //foreach (ScoreDoc sd in tds.ScoreDocs)
                //{
                //    //��ѯƥ�����
                //    Console.WriteLine(sd.Score);

                //    Document doc = searcher.Doc(sd.Doc);
                //    Console.WriteLine(doc.Get("body"));
                //    Console.WriteLine(doc.Get("id"));
                //    var date = doc.Get("date");
                //}
            }
        }

        [TestMethod]
        public void TestLogger()
        {
            Global.ServiceProvider.GetService<ILogger<Test>>().LogDebug("LogDebug");
            Global.ServiceProvider.GetService<ILogger<Test>>().LogError(new Exception("abc") , "�����쳣");
            Global.ServiceProvider.GetService<ILogger<Test>>().LogInformation("normal");
            Thread.Sleep(10000);
        }



        [TestMethod]
        public void WriteMuilteLog()
        {
            var logService = _serviceProvider.GetService<LogService>();
            for (int i = 0; i < 100000000; i++)
            {
               
                logService.WriteLog(new Jack.RemoteLog.WebApi.Dtos.WriteLogModel
                {
                    ApplicationContext = "UnitTest",
                    SourceContext = "test",
                    Content = "Exchange:Exchange.BlockScan �յ�BlockScan��Ϣ��{\"BlockNumber\":43325451,\"Txid\":\"04b394121551767dd7237d8fcaf84eab31f102f3bd03122b94bf784581217ec5\",\"Amount\":99999.0,\"Time\":\"1970-01-01T00:00:00+00:00\",\"Confirmations\":3,\"Valid\":true,\"PropertyId\":\"TR7NHqjeKQxGTCi8q8ZY4pL8otSzgjLj6t\",\"CoinType\":202,\"TronTransactionType\":\"TriggerSmartContract\",\"ContractRet\":\"SUCCESS\",\"Fee\":0.0,\"SenderAddress\":\"TBA6CypYJizwA9XdC7Ubgc5F1bxrQ7SqPt\",\"ReceivedAddress\":\"TVKCgFfuuzu11idqBjqMoSUDvmVQYnJWwY\",\"Coin\":\"USDT\"}",
                    Level = Microsoft.Extensions.Logging.LogLevel.Debug,
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
                });

                if(i % 1000 == 0)
                    Thread.Sleep(1);
            }
        }
    }

    public class LuceneAnalyze
    {
        #region AnalyzerKey
        /// <summary>
        /// ��������keyword�ִ�
        /// </summary>
        /// <param name="keyword"></param>
        /// <returns></returns>
        public static string[] AnalyzerKey(string keyword)
        {
            Analyzer analyzer = new PanGuAnalyzer();
            QueryParser parser = new QueryParser(Lucene.Net.Util.LuceneVersion.LUCENE_48, "title", analyzer);
            Query query = parser.Parse(CleanKeyword(keyword));
            if (query is TermQuery)
            {
                Term term = ((TermQuery)query).Term;
                return new string[] { term.Text() };
            }
            else if (query is PhraseQuery)
            {
                Term[] term = ((PhraseQuery)query).GetTerms();
                return term.Select(t => t.Text()).ToArray();
            }
            else if (query is BooleanQuery)
            {
                BooleanClause[] clauses = ((BooleanQuery)query).GetClauses();
                List<string> analyzerWords = new List<string>();
                foreach (BooleanClause clause in clauses)
                {
                    Query childQuery = clause.Query;
                    if (childQuery is TermQuery)
                    {
                        Term term = ((TermQuery)childQuery).Term;
                        analyzerWords.Add(term.Text());
                    }
                    else if (childQuery is PhraseQuery)
                    {
                        Term[] term = ((PhraseQuery)childQuery).GetTerms();
                        analyzerWords.AddRange(term.Select(t => t.Text()));
                    }
                }
                return analyzerWords.ToArray();
            }
            else
            {
                return new string[] { keyword };
            }
        }

        /// <summary>
        /// ����ͷβand or �ؼ���
        /// </summary>
        /// <param name="keyword"></param>
        /// <returns></returns>
        private static string CleanKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            { }
            else
            {
                bool isClean = false;
                while (!isClean)
                {
                    keyword = keyword.Trim();
                    if (keyword.EndsWith(" AND"))
                    {
                        keyword = string.Format("{0}and", keyword.Remove(keyword.Length - 3, 3));
                    }
                    else if (keyword.EndsWith(" OR"))
                    {
                        keyword = string.Format("{0}or", keyword.Remove(keyword.Length - 2, 2));
                    }
                    else if (keyword.StartsWith("AND "))
                    {
                        keyword = string.Format("and{0}", keyword.Substring(3));
                    }
                    else if (keyword.StartsWith("OR "))
                    {
                        keyword = string.Format("or{0}", keyword.Substring(2));
                    }
                    else if (keyword.Contains(" OR "))
                    {
                        keyword = keyword.Replace(" OR ", " or ");
                    }
                    else if (keyword.Contains(" AND "))
                    {
                        keyword = keyword.Replace(" AND ", " and ");
                    }
                    else
                        isClean = true;
                }
            }
            return keyword;
            return QueryParser.Escape(keyword);
        }
        #endregion AnalyzerKey
    }
    class TestObject:IDisposable
    {
        public TestObject()
        {
            Debug.WriteLine("���й��캯��");
        }

        public void Dispose()
        {
            Debug.WriteLine("�ͷ���");
        }
    }
}