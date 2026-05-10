using System.Formats.Cbor;
using Disruptor.Surreal.Cbor;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// The wire-level request envelope. Encoded as a CBOR map matching the shape consumed
/// by the official server: <c>{ id, method, params?, txn? }</c>.
/// </summary>
internal readonly struct RpcRequest(long id, string method, SurrealValue? @params, Guid? txnId)
{
    public long Id { get; } = id;
    public string Method { get; } = method;
    public SurrealValue? Params { get; } = @params;
    public Guid? TxnId { get; } = txnId;

    /// <summary>Encodes this request to a CBOR byte array.</summary>
    public byte[] Encode()
    {
        var writer = new CborWriter(CborConformanceMode.Lax, convertIndefiniteLengthEncodings: false);

        var fieldCount = 2; // id + method
        if (Params is not null) fieldCount++;
        if (TxnId is not null) fieldCount++;

        writer.WriteStartMap(fieldCount);

        writer.WriteTextString("id");
        writer.WriteInt64(Id);

        writer.WriteTextString("method");
        writer.WriteTextString(Method);

        if (Params is not null)
        {
            writer.WriteTextString("params");
            SurrealCborValueWriter.Write(writer, Params);
        }

        if (TxnId is { } txn)
        {
            writer.WriteTextString("txn");
            SurrealCborValueWriter.Write(writer, new SurrealUuidValue(txn));
        }

        writer.WriteEndMap();
        return writer.Encode();
    }
}
