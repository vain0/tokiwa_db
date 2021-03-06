﻿namespace TokiwaDb.Core.Test

open System
open System.IO
open Persimmon
open Persimmon.Syntax.UseTestNameByReflection
open TokiwaDb.Core

module DatabaseTest =
  let dbDir       = DirectoryInfo(@"__unit_test_db")

  if dbDir.Exists then
    dbDir.Delete((* recusive =*) true)

  let mutable savedRevision = 0L
  let insertedRow = [| String "Miku"; Int 16L|]

  let createTest =
    test {
      use db      = new DirectoryDatabase(dbDir)
      let rev     = db.ImplTransaction.RevisionServer

      let persons =
        let schema =
          { TableSchema.empty "persons" with
              Fields = [| Field.string "name"; Field.int "age" |]
              Indexes = [| HashTableIndexSchema [| 1 |] |]
          }
        in db.CreateTable(schema)

      let actual = db.ImplTables |> Seq.map (fun table -> table.Name) |> Seq.toList
      do! actual |> assertEquals [persons.Name]

      let _ = persons.Insert([| insertedRow |])

      savedRevision <- rev.Current
      return ()
    }

  let reopenTest =
    test {
      use db      = new DirectoryDatabase(dbDir)
      /// Revision number should be saved.
      do! db.CurrentRevisionId |> assertEquals savedRevision
      /// Tables should be loaded.
      let tables  = db.ImplTables |> Seq.filter (Mortal.isAliveAt savedRevision)
      let actual  = tables |> Seq.map (fun table -> table.Name) |> Seq.toList
      do! actual |> assertEquals ["persons"]
      /// Inserted rows should be saved.
      let persons = tables |> Seq.find (fun table -> table.Name = "persons")
      let actual  = persons.Relation(savedRevision).RecordPointers |> Seq.head |> db.Storage.Derefer
      do! actual |> assertEquals (Array.append [| Int 0L |] insertedRow)
    }
