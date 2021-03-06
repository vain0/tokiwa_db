﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// ここでは、常磐DBを C# から使用する方法を説明します。
// Here we describe how to use Tokiwa DB in C#.

// 初めに、 TokiwaDb.Core.dll と TokiwaDb.CodeFirst.dll への参照をプロジェクトに追加する必要があります。
// First of all, you need to add references to TokiwaDb.Core.dll and TokiwaDb.CodeFirst.dll.

namespace TokiwaDb.CodeFirst.Sample.CSharp
{
    // データベースを作成するために、 TokiwaDb.CodeFirst.Model クラスを継承した「モデル」クラスを定義します。
    // これらのインスタンスはレコードを表現します。
    // ここでは例として、モデルクラス Person と Song を定義します。
    // To create database, you must define "model" classes, inheriting TokiwaDb.CodeFirst.Model, whose instances represent records. For example:
    public class Person
        : Model
    {
        // モデルクラスのセッター (setter) を持つプロパティは、レコードのフィールドを表すとみなされます。
        // これらのプロパティの型は long, double, DateTime, string, byte[], Lazy<string>, Lazy<byte[]> のいずれかでなければなりません。
        // メモ: int, char[] などは使えません。
        // Properties with setter of model classes are considered to represent fields of a record.
        // These properties must be of long, double, DateTime, string, byte[], Lazy<string> or Lazy<byte[]>.
        // Note: int, char[], etc. are NOT allowed.
        public string Name { get; set; }
        public long Age { get; set; }

        // 必要に応じて、その他の定義を含めてもかまいません。
        // And other definitions if necessary.
        public override string ToString()
        {
            return string.Format("{0} ({1})", Name, Age);
        }
    }

    public class Song
        : Model
    {
        public string Title { get; set; }
        public Lazy<string> VocalName { get; set; }
    }

    // 加えて、「データベース文脈クラス」と呼ばれるクラスが1つ必要になります。
    // これは以下の規約に従う必要があります:
    //   - Database という名前の TokiwaDb.CodeFirst.Database 型のプロパティを持つ。
    //   - 各モデルクラス M につき、型 Table<M> のプロパティを持つ。
    //   - Database と各モデルクラスからなる1つのコンストラクターを持つ。
    // 規約に違反している場合は、実行時に例外が投げられます。
    // メモ: F# では、レコード型を使うことで、記述の冗長さを軽減することが可能です。
    // Additionally you also need to define a "database context class which meets the following convention:
    //   - it has a property named "Database" of TokiwaDb.CodeFirst.Database;
    //   - it has, for each model class M, a property of Table<M>; and
    //   - it has a constructor which takes a Database and model classes.
    // Otherwise, a runtime exception will be thrown.
    // Note: Record types prevent this redudancy of code in F#.
    public class TutorialDbContext
        : IDisposable
    {
        public Database Database { get; private set; }
        public Table<Person> Persons { get; private set; }
        public Table<Song> Songs { get; private set; }

        public TutorialDbContext(Database db, Table<Person> persons, Table<Song> songs)
        {
            Database = db;
            Persons = persons;
            Songs = songs;
        }

        // 規約と無関係な定義が含まれていてもかまいません。
        // It's okay to add other definitions unrelated to the convention.
        public long CurrentRevisionId
        {
            get { return Database.CurrentRevisionId; }
        }

        // Database 型は IDisposable を実装しています。
        // Database implements IDisposable. (STUB)
        void IDisposable.Dispose()
        {
            Database.Dispose();
        }
    }

    //-------------------------------------------

    [TestClass]
    public class Tutorial
    {
        public TutorialDbContext OpenDatabase()
        {
            // データベースを作成したり、データベースに接続したりするには、DbConfig を使用します。
            // 以下にその手順を説明します。
            // To connect or create a database, use DbConfig. Like this:
            var dbConfig = new DbConfig<TutorialDbContext>();

            // 一意性制約が使用できます。
            // メモ: 実際にはハッシュ表の索引が作られますが、それを使用する手段は未実装です。
            // Unique constraints are available.
            // Note: Actually a hashtable index is created for each unique contraints, you can't use it yet.
            dbConfig.Add<Person>(UniqueIndex.Of<Person>(p => p.Name));

            // そして、OpenMemory メソッドを呼び出して、インメモリーのデータベースを生成します。
            // Then invoke OpenMemory to create an in-memory database.
            return dbConfig.OpenMemory("sample_db");

            // ディスクベースのデータベースに対しては、代わりに OpenDirectory メソッドを使用してください。
            // これは OpenMemory とは異なり、既存のデータベースを開くことができます。
            // 重要: データベースのディレクトリーがすでに存在し、しかしモデルクラスが異なっている場合、
            //       すべてのテーブルが Drop され、改めて新しいテーブルが作られます。
            // Use OpenDirectory for disk-based one instead.
            // Unlike OpenMemory, OpenDirectory can open an exsiting database.
            // Note: If the database directory exists but model classes have changed,
            //       all tables are dropped and new tables are created again.

            //return dbConfig.OpenDirectory(new System.IO.DirectoryInfo(@"path/to/directory"));
        }

        [TestMethod]
        public void InsertSample()
        {
            // データベースを作成、あるいは接続します。
            // Open (connect) or create the database.
            using (TutorialDbContext db = OpenDatabase())
            {
                // テーブルにアクセスするには、単にデータベース文脈クラスのプロパティを使用します。
                // プロパティが返す Table<M> 型のオブジェクトが、モデルクラス M に対応するテーブルにアクセスする手段を提供します。
                // To access to tables, just use properties of your database context class.
                // The returned object of Table<M> provides the way to access to the corresponding table to the model class M.
                Table<Person> persons = db.Persons;

                // Insert メソッドは、モデルクラスのインスタンスをレコードとしてテーブルに挿入するメソッドです。
                // トランザクションの外側では、この処理は即座に反映されます。
                // メモ: 挿入されるインスタンスの Id プロパティは無効値 (あるいは既定値) でなければなりません。
                // メモ: 一意性制約に違反する場合、例外が投げられます。
                // Insert method inserts a model instance as a record to the table.
                // This effects immediately out of transactions.
                // Note: Id property of the inserted instance must be invalid (or default).
                // Note: This may throw an exception because of uniqueness constraints.
                var person = new Person() { Name = "Miku", Age = 16L };
                Assert.IsTrue(person.Id < 0L);
                persons.Insert(person);

                // Insert メソッドの後、挿入されるインスタンスの Id プロパティの値が、それの Id に設定されます。
                // これはトランザクションの中でも同様です。
                // After the Insert method, Id property of the inserted instance is set to its Id
                // both in or out of transactions.
                Assert.AreEqual(0L, person.Id);

                // 現在の Person テーブルには1個のレコードが含まれていることになります。
                // Now the Person table contains one record.
                Assert.AreEqual(1L, persons.CountAllRecords);
            }
        }

        // 後のサンプルのため、サンプルデータを含むデータベースを作成する関数を定義しておきます。
        // For the later samples, we define a helper function which creates a database with sample data.
        public TutorialDbContext CreateSampleDatabase()
        {
            var db = OpenDatabase();
            db.Persons.Insert(new Person() { Name = "Miku", Age = 16L });
            db.Persons.Insert(new Person() { Name = "Yukari", Age = 18L });

            db.Songs.Insert(new Song() { Title = "Rollin' Girl", VocalName = new Lazy<string>(() => "Miku") });
            db.Songs.Insert(new Song() { Title = "Sayonara Chainsaw", VocalName = new Lazy<string>(() => "Yukari") });
            return db;
        }

        [TestMethod]
        public void ItemSample()
        {
            using (var db = CreateSampleDatabase())
            {
                // Table<M>.Item プロパティの getter (インデクサー) は、与えられた ID を持つレコードを取得します。
                // The getter of Table<M>.Item property (indexer) fetches the record with the given id.
                var miku = db.Persons[0L];
                Assert.AreEqual("Miku", miku.Name);
            }
        }

        [TestMethod]
        public void ItemsSample()
        {
            using (var db = CreateSampleDatabase())
            {
                // Table<M>.Items は削除されていないすべてのインスタンスを IEnumerable<M> として返します。
                // このシーケンスは、レコードの読み込みとインスタンスの生成を必要に応じて行います。
                // Table<M>.Items returns all "live" instances as an IEnumerable<M>.
                // The sequence reads and constructs model instances on demand.
                IEnumerable<Person> items = db.Persons.Items;

                // LINQ to Object が使用できます。
                // You can use "LINQ to Object".
                Assert.AreEqual("Miku", items.ElementAt(0).Name);

                // クエリー式は、複雑なクエリーを書くときの助けになります。
                // Query expressions help you to write complex queries.
                var queryResult =
                    from person in db.Persons.Items
                    join song in db.Songs.Items on person.Name equals song.VocalName.Value
                    where person.Age >= 18L
                    select new { Name = person.Name, Title = song.Title, Age = person.Age };
                Assert.AreEqual(1, queryResult.Count());
                Assert.AreEqual(new { Name = "Yukari", Title = "Sayonara Chainsaw", Age = 18L }, queryResult.First());
            }
        }

        [TestMethod]
        public void RemoveSample()
        {
            using (var db = CreateSampleDatabase())
            {
                // 後で「現時点のデータベース」を参照するために、今のリビジョン番号を記録しておきます。
                // Save the current revision number of the database
                // to access to the database with the current state later.
                var savedRevisionId = db.Database.CurrentRevisionId;

                // Remove メソッドは、指定された Id を持つレコードをテーブルから除去します。
                // Remove method removes the record with the given Id from the table.
                var miku = db.Persons.Items.First();
                Assert.AreEqual("Miku", miku.Name);
                db.Persons.Remove(miku.Id);

                // 現在の Person テーブルには、Miku という名前のデータがなくなっていることになります。
                // The Person table no longer contains Miku.
                Assert.IsFalse(db.Persons.Items.Any(p => p.Name == "Miku"));

                // しかし、Remove メソッドは論理削除を行うだけです。
                // AllItems と savedRevisionId を使うことで、Miku のデータを再び得ることができます。
                // AllItems は、削除されたものも含めて、テーブルに含まれるすべてのインスタンスを返します。
                // あるインスタンスがリビジョン t において有効かどうかは、IsLiveAt(t) の真偽値で判断します。
                // The Remove method, however, performs logical deletion.
                // You can get Miku again by using AllItems and savedRevisionId.
                // AllItems returns all instances in the table including removed ones.
                // Each of those is valid at the revision t if and only if IsLiveAt(t) returns true.
                var items = db.Persons.AllItems.Where(p => p.IsLiveAt(savedRevisionId));
                Assert.AreEqual(miku.ToString(), items.First().ToString());
            }
        }

        [TestMethod]
        public void TransactionSample()
        {
            using (var db = CreateSampleDatabase())
            {
                // Database.Transaction はトランザクションオブジェクトを返します。
                // これはデータベースごとに一意なオブジェクトです。
                // 始め、トランザクションは開始されていないので、前述のとおりすべての操作 (Insert, Remove) は即座に反映されます。
                // Database.Transaction returns the transaction object.
                // It's singleton for each database.
                // At first no transactions are beginning,
                // so all operations (Insert, Remove) affects immediately as above.
                var transaction = db.Database.Transaction;

                try
                {
                    // Transaction.Begin は新しいトランザクションを開始します。
                    // メモ: ネストされたトランザクションを開始することも可能です。
                    // Transaction.Begin begins new transaction.
                    // Note: You can also begin nested transactions.
                    transaction.Begin();

                    // 例として、いろいろ操作を行います。
                    // Do operations for example...
                    {
                        var rin = new Person() { Name = "Rin", Age = 14L };
                        db.Persons.Insert(rin);
                        Assert.AreEqual(2L, rin.Id);

                        var firstPerson = db.Persons.Items.First();
                        db.Persons.Remove(firstPerson.Id);

                        // トランザクション中の操作は、すぐには反映されません。
                        // Operations during a transaction don't affect immediately.

                        // 挿入がまだ行われていないこと (Not inserted yet.)
                        Assert.IsFalse(db.Persons.Items.Any(p => p.Id == rin.Id));
                        // 除去がまだ行われていないこと (Not removed yet.)
                        Assert.IsTrue(db.Persons.Items.Any(p => p.Id == firstPerson.Id));
                    }

                    // Transaction.Commit は現在のトランザクションを終了させます。
                    // そのトランザクション中に登録されたすべての操作は、ここで実行されます (ネストされたトランザクションでない場合)。
                    // Transaction.Commit ends the current transaction.
                    // All operations registered during the transaction are performed now unless the transaction is nested.
                    transaction.Commit();
                }
                catch (Exception)
                {
                    // Transaction.Rollback も現在のトランザクションを終了させます。
                    // トランザクション中に登録されたすべての操作は破棄されます。
                    // Transaction.Rollback method also ends the current transaction.
                    // All operations registered during the transaction are just discarded.
                    transaction.Rollback();
                    throw;
                }
            }
        }
    }
}
