﻿namespace TokiwaDb.Core

open System
open System.Collections
open System.Collections.Generic
open Chessie.ErrorHandling

[<AutoOpen>]
module Types =
  /// A pointer to a storage.
  type pointer = int64

  /// Identity number.
  type Id = int64

  /// A version number of database.
  /// The initial value is 0.
  type RevisionId = int64

  type [<AbstractClass>] RevisionServer() =
    abstract member Current: RevisionId
    abstract member Next: RevisionId
    abstract member Increase: unit -> RevisionId

  type IMortal =
    abstract member Birth: option<RevisionId>
    abstract member Death: option<RevisionId>

  type [<AbstractClass>] Mortal<'x>() =
    abstract member Value: 'x
    interface IMortal with
      override this.Birth = None
      override this.Death = None

  type Name = string

  type Value =
    | Int           of int64
    | Float         of float
    | String        of string
    | Time          of DateTime

  type ValuePointer =
    | PInt           of int64
    | PFloat         of float
    | PString        of pointer
    | PTime          of DateTime

  type ValueType =
    | TInt
    | TFloat
    | TString
    | TTime

  type Field =
    | Field         of Name * ValueType

  type IndexSchema =
    | HashTableIndexSchema      of array<int>

  type TableSchema =
    {
      Name          : Name
      /// Except for id field.
      Fields        : array<Field>
      Indexes       : array<IndexSchema>
    }

  type Record =
    array<Value>

  type RecordPointer =
    array<ValuePointer>

  /// A storage which stores values with size more than 64bit (e.g. string, blob).
  /// Data in storage must be immutable and unique.
  type [<AbstractClass>] Storage() =
    abstract member Derefer: ValuePointer -> Value
    abstract member Store: Value -> ValuePointer

  type [<AbstractClass>] Relation() =
    abstract member Fields: array<Field>
    abstract member RecordPointers: seq<RecordPointer>

    abstract member Filter: (RecordPointer -> bool) -> Relation
    abstract member Projection: array<Name> -> Relation
    abstract member Rename: Map<Name, Name> -> Relation
    abstract member Extend: array<Field> * (RecordPointer -> RecordPointer) -> Relation
    abstract member JoinOn: array<Name> * array<Name> * Relation -> Relation

    abstract member ToTuple: unit -> array<Field> * array<RecordPointer>

  [<RequireQualifiedAccess>]
  type Error =
    | WrongRecordType     of array<Field> * Record
    | DuplicatedRecord    of RecordPointer
    | InvalidId           of Id

  type Operation =
    | InsertRecords       of tableId: Id * array<RecordPointer>
    | RemoveRecords       of tableId: Id * array<Id>

  type [<AbstractClass>] Transaction() =
    abstract member BeginCount: int
    abstract member Operations: seq<Operation>
    abstract member RevisionServer: RevisionServer

    abstract member Begin: unit -> unit
    abstract member Add: Operation -> unit
    abstract member Commit: unit -> unit
    abstract member Rollback: unit -> unit

    abstract member SyncRoot: obj

  type [<AbstractClass>] HashTableIndex() =
    abstract member Projection: RecordPointer -> RecordPointer
    abstract member TryFind: RecordPointer -> option<Id>

    abstract member Insert: RecordPointer * Id -> unit
    abstract member Remove: RecordPointer -> bool

  type [<AbstractClass>] Table() =
    abstract member Id: Id
    abstract member Schema: TableSchema
    abstract member Relation: RevisionId -> Relation
    abstract member Database: Database
    abstract member Indexes: array<HashTableIndex>

    abstract member RecordById: Id -> option<Mortal<RecordPointer>>
    abstract member ToSeq: unit -> seq<Mortal<RecordPointer>>

    abstract member PerformInsert: array<RecordPointer> -> unit
    abstract member PerformRemove: array<Id> -> unit
    abstract member Insert: array<Record> -> Result<array<Id>, Error>
    abstract member Remove: array<Id> -> Result<unit, Error>
    abstract member Drop: unit -> unit

    member this.Name = this.Schema.Name

  and [<AbstractClass>] Database() =
    abstract member Name: string

    abstract member Transaction: Transaction
    abstract member Storage: Storage

    abstract member Tables: RevisionId -> seq<Table>

    abstract member CreateTable: TableSchema -> Table
    abstract member DropTable: Id -> bool
    abstract member Perform: array<Operation> -> unit

    member this.CurrentRevisionId = this.Transaction.RevisionServer.Current
