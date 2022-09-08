using Jack.RemoteLog;
using Jack.RemoteLog.WebApi;
using Jack.RemoteLog.WebApi.Applications;
using Jack.RemoteLog.WebApi.AutoMissions;
using Lucene.Net.Analysis.PanGu;
using Lucene.Net.Analysis;
using Microsoft.Data.Sqlite;
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
            var context = @"�й�רҵIT����CSDN (Chinese Software Developer Network) ������1999�꣬������Ϊ�й�����������ṩ֪ʶ����������ѧϰ��ְҵ��չ��ȫ�������ڷ���
����ӵ�У�רҵ������IT���������� CSDN.NET���ƶ��˿�����ר��APP�� CSDN APP��CSDNѧԺAPP����ý�����΢�Ź��ںţ�CSDN��Ѷ������������GitChat��CSDNѧԺ��AI�Ƽ���Ӫ����������Ӫ��CSDN�Ƽ��㡢GitChat��Ʒ�Ρ��˹�����ͷ����CSDN��ҵ��Ƹ��IT������ѵѧϰƽ̨�� CSDNѧԺ������֪ʶ�ƶ������� GitChat���˹������������� TinyMind��Ȩ��IT��������ƽ̨��������Ա��+GitChat��IT������Դ���񣺿��񸣿�˹��IT����������ƽ̨��CTO���ֲ���";
            //Ҳ��ָ�������ֶ���Ϣ�磺id
            var id = "77889111111111111111";

            IndexWriter iw = new IndexWriter(FSDirectory.Open(INDEX_DIR), analyzer, true, IndexWriter.MaxFieldLength.LIMITED);

            //�洢�ĵ��������
            Document doc = new Document();
            //ָ���Ϸ����������ݴ洢��key�Լ�����ģʽ
            doc.Add(new Field("body", context, Field.Store.YES, Field.Index.ANALYZED));
            //ָ���Ϸ�������id�洢��key�Լ�����ģʽ
            doc.Add(new Field("id", id, Field.Store.YES, Field.Index.NOT_ANALYZED));

            //��������ɵ����ݴ洢
            iw.AddDocument(doc);

            iw.Commit();
            iw.Optimize();
            iw.Dispose();
        }

        [TestMethod]
        public void TestSearchPanGu()
        {
            var keyword = "������Ϊ�й�����������ṩ֪ʶ����";
            DirectoryInfo INDEX_DIR = new DirectoryInfo(AppContext.BaseDirectory + "index");
            Analyzer analyzer = new PanGuAnalyzer(); //MMSegAnalyzer //StandardAnalyzer

            IndexSearcher searcher = new IndexSearcher(FSDirectory.Open(INDEX_DIR), true);
            //����Ҫ������Key
            QueryParser qp = new QueryParser(Lucene.Net.Util.Version.LUCENE_30, "body", analyzer);
            //�Լ������ݽ��зִ�
            Lucene.Net.Search.PhraseQuery query = (Lucene.Net.Search.PhraseQuery)qp.Parse(keyword);
            Lucene.Net.Search.PhraseQuery realquery = new PhraseQuery();
            var terms = query.GetTerms();
            foreach(Term term in terms)
            {
                realquery.Add(new Term(term.Field,term.Text + "*"));
            }

            Console.WriteLine("�ִʽ�� {0}", query);

            //��ѯ�ִ����ݣ��Լ����÷��ؼ������������
            TopDocs tds = searcher.Search(realquery, 1000);
            Console.WriteLine("���������: " + tds.TotalHits);
            foreach (ScoreDoc sd in tds.ScoreDocs)
            {
                //��ѯƥ�����
                Console.WriteLine(sd.Score);

                Document doc = searcher.Doc(sd.Doc);
                Console.WriteLine(doc.Get("body"));
                Console.WriteLine(doc.Get("id"));
            }
            searcher.Dispose();
        }

        [TestMethod]
        public void TestFts5()
        {
            var m_dbConnection = new SqliteConnection(@"Data Source=.\test.db");
            m_dbConnection.Open();

            //string sql = $"select * from message where message match 'body:ȷ�Ͻ���*'";
            //SqliteCommand command = new SqliteCommand(sql, m_dbConnection);
            //var reader = command.ExecuteReader();
            //while (reader.Read())
            //{
            //    var num = reader[0];
            //    var name = reader[1];
            //}


            //for (int i = 0; i < 10000; i++)
            //{
            //    string sql = $"insert into message (title,body) values ({i},'ȷ�Ͻ���Transaction-ETH,txid0x{i}a3670cf72abc54cfcc093796790f7958ed490db705d8d9eb99e47914be2ce3 r:0x2319db93bf503f850d623b3e67e267685f5f2681 s:0xb5d85cbf7cb3ee0d56b3bb207d5fc4b82f43f511 a:0.01271899')";
            //    SqliteCommand command = new SqliteCommand(sql, m_dbConnection);
            //    command.ExecuteNonQuery();
            //}

            string sql = "CREATE VIRTUAL TABLE message USING fts3(title, body, tokenize=ICU)";
            SqliteCommand command = new SqliteCommand(sql, m_dbConnection);
            command.ExecuteNonQuery();
            m_dbConnection.Close();
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
            for (int i = 0; i < 1000000; i++)
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