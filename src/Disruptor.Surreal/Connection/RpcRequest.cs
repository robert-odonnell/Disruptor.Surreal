using System.Formats.Cbor;
using Disruptor.Surreal.Cbor;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// The wire-level request envelope. Encoded as a CBOR map matching the shape consumed
/// by the official server: <c>{ id, method, params?, txn? }</c>.
/// </summary>
internal readonly struct RpcRequest
{
    public long Id { get; }
    public string Method { get; }
    public Value? Params { get; }
    public Guid? TxnId { get; }

    public RpcRequest(long id, string method, Value? @params, Guid? txnId)
    {
        Id = id;
        Method = method;
        Params = @params;
        TxnId = txnId;
    }

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
            CborValueWriter.Write(writer, Params);
        }

        if (TxnId is { } txn)
        {
            writer.WriteTextString("txn");
            CborValueWriter.Write(writer, new UuidValue(txn));
        }

        writer.WriteEndMap();
        return writer.Encode();
    }
}
