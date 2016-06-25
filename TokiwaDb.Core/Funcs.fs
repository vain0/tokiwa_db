﻿namespace TokiwaDb.Core

open System
open System.IO
open System.Threading
open FsYaml

module Value =
  let toType =
    function
    | Int _    -> TInt
    | Float _  -> TFloat
    | String _ -> TString
    | Time _   -> TTime

module ValuePointer =
  let ofUntyped type' (p: int64) =
    match type' with
    | TInt      -> PInt p
    | TFloat    -> PFloat (BitConverter.Int64BitsToDouble p)
    | TString   -> PString p
    | TTime     -> PTime (DateTime.FromBinary(p))

  let toUntyped =
    function
    | PInt p    -> p
    | PFloat d  -> BitConverter.DoubleToInt64Bits(d)
    | PString p -> p
    | PTime t   -> t.ToBinary()

  let serialize vp =
    BitConverter.GetBytes(vp |> toUntyped)

  let hash vp =
    vp |> toUntyped

  let serializer =
    FixedLengthUnionSerializer<ValuePointer>
      ([|
        Int64Serializer()
        FloatSerializer()
        Int64Serializer()
        DateTimeSerializer()
      |])

module Record =
  let toType record =
    record |> Array.map Value.toType

module RecordPointer =
  let hash recordPointer =
    recordPointer |> Array.map ValuePointer.hash |> Array.hash |> int64

  let serializer len =
    FixedLengthArraySerializer(ValuePointer.serializer, len)

  let tryId recordPointer =
    match recordPointer |> Array.tryHead with
    | Some (PInt recordId) -> recordId |> Some
    | _ -> None

  let dropId (recordPointer: RecordPointer) =
    recordPointer.[1..]

  let readFromStream fields (stream: Stream) =
    [|
      for Field (_, type') in fields do
        yield stream |> Stream.readInt64 |> ValuePointer.ofUntyped type'
    |]

  let writeToStream (stream: Stream) recordPointer =
    for valuePointer in recordPointer do
      stream |> Stream.writeInt64 (valuePointer |> ValuePointer.toUntyped)

module Field =
  let toType (Field (_, type')) =
    type'

  let int name =
    Field (name, TInt)

  let float name =
    Field (name, TFloat)

  let string name =
    Field (name, TString)

  let time name =
    Field (name, TTime)

module TableSchema =
  let empty name =
    {
      Name              = name
      Fields            = [||]
      Indexes           = [||]
    }

  let toFields (schema: TableSchema) =
    Array.append [| Field ("id", TInt) |] schema.Fields

type MemoryRevisionServer(_id: RevisionId) =
  inherit RevisionServer()
  let mutable _id = _id

  new() =
    MemoryRevisionServer(0L)

  override this.Current =
    _id

  override this.Next =
    _id + 1L

  override this.Increase() =
    Interlocked.Increment(& _id)
