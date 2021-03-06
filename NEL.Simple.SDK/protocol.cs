﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NEL.Simple.SDK.Helper;

namespace NEL.Simple.SDK
{
    public static class Protocol_UseSnapShot
    {
        public static NetMessage CreateSendMsg(string id = "")
        {
            NetMessage netMessage = NetMessage.Create("_db.usesnapshot", null, id);
            return netMessage;
        }

        public static UInt64 PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.snapid;
        }
    }

    public static class Protocol_DisposeSnapShot
    {
        public static NetMessage CreateSendMsg(string id = "")
        {
            NetMessage netMessage = NetMessage.Create("_db.disposeSnapshot", null,id);
            return netMessage;
        }
    }

    public static class Protocol_CreateWriteBatch
    {
        public static NetMessage CreateSendMsg(string id = "")
        {
            NetMessage netMessage = NetMessage.Create("_db.CreateWriteBatch", null, id);
            return netMessage;
        }

        public static UInt64 PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.wbid;
        }
    }

    public static class Protocol_Put
    {
        public static NetMessage CreateSendMsg(UInt64 wbid, byte[] tableid,byte[] key,byte[] value,string id = "")
        {
            var p = new Param() {wbid = wbid,tableid = tableid,key = key,value = value };
            NetMessage netMessage = NetMessage.Create("_db.put", p,id);
            return netMessage;
        }
    }

    public static class Protocol_Delete
    {
        public static NetMessage CreateSendMsg(UInt64 wbid,byte[] tableid,byte[] key,string id = "")
        {
            var p = new Param() { wbid = wbid, tableid = tableid, key = key};
            NetMessage netMessage = NetMessage.Create("_db.delete", p, id);
            return netMessage;
        }
    }

    public static class Protocol_Write
    {
        public static NetMessage CreateSendMsg(UInt64 wbid,string id)
        {
            var p = new Param() { wbid = wbid};
            NetMessage netMessage = NetMessage.Create("_db.write",p, id);
            return netMessage;
        }
    }

    public static class Protocol_CreateIterator
    {
        public static NetMessage CreateSendMsg( UInt64 snapid,byte[] tableid , byte[] beginKey = null,byte[] endKey =null,string id="")
        {
            var p = new Param() { snapid = snapid ,key = beginKey,value = endKey,tableid = tableid };
            NetMessage netMessage = NetMessage.Create("_db.snapshot.newiterator", p,id);
            return netMessage;
        }

        public static UInt64 PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.itid;
        }
    }

    public static class Protocol_IteratorCurrent
    {
        public static NetMessage CreateSendMsg(UInt64 itid,string id = "")
        {
            var p = new Param() { itid = itid};
            NetMessage netMessage = NetMessage.Create("_db.iterator.current",p, id);
            return netMessage;
        }

        public static byte[] PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.value;
        }
    }

    public static class Protocol_IteratorNext
    {
        public static NetMessage CreateSendMsg(UInt64 itid,string id = "")
        {
            var p = new Param() { itid = itid };
            NetMessage netMessage = NetMessage.Create("_db.iterator.next",p, id);
            return netMessage;
        }

        public static bool PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.result;
        }
    }

    public static class Protocol_IteratorSeekToFirst
    {
        public static NetMessage CreateSendMsg(UInt64 itid, string id = "")
        {
            var p = new Param() { itid = itid };
            NetMessage netMessage = NetMessage.Create("_db.iterator.seektofirst",p ,id);
            return netMessage;
        }
        public static bool PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.result;
        }
    }

    public static class Protocol_IteratorReset
    {
        public static NetMessage CreateSendMsg(UInt64 itid, string id = "")
        {
            var p = new Param() { itid = itid };
            NetMessage netMessage = NetMessage.Create("_db.iterator.reset", p,id);
            return netMessage;
        }
        public static bool PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.result;
        }
    }

    public static class Protocol_GetValue
    {
        public static NetMessage CreateSendMsg(byte[] tableid, byte[] key,string id = "", UInt64 snapid = 0, bool useServerSnap =false)
        {
            var p = new Param() { snapid = snapid,key = key,tableid = tableid};
            NetMessage netMessage = NetMessage.Create("_db.snapshot.getvalue",p ,id);
            if (useServerSnap)
                netMessage = NetMessage.Create("_db.getvalue", p,id);
            return netMessage;
        }

        public static byte[] PraseRecvMsg(NetMessage msg)
        {
            return msg.Param.value;
        }
    }
    /*
    public static class Protocol_GetStorage
    {
        public class message
        {
            public byte[] key;
            public byte[] value;
        }

        public static NetMessage CreateSendMsg(byte[] key, string id = "", UInt64 snapid = 0, bool useServerSnap = false)
        {
            Param param = new Param() { snapid = 0,tableid = Prefixes.ST_Storage.ToBytes(), key =key };
            NetMessage netMessage = NetMessage.Create("_db.snapshot.getvalue",param ,id);
            if (useServerSnap)
                netMessage = NetMessage.Create("_db.getvalue", param, id);
            return netMessage;
        }

        public static message PraseRecvMsg(NetMessage netMessage)
        {
            var msg = new message() { key = netMessage.Param.key, value = netMessage.Param.value };
            return msg;
        }
    }

    public static class Protocol_GetBlock
    {
        public static NetMessage CreateSendMsg(byte[] key, string id = "", UInt64 snapid = 0, bool useServerSnap = false)
        {
            Param param = new Param() {snapid = snapid, tableid = Prefixes.DATA_Block.ToBytes(), key =key};
            NetMessage netMessage = NetMessage.Create("_db.snapshot.getvalue", param, id);
            if (useServerSnap)
                netMessage = NetMessage.Create("_db.getvalue", param, id);
            return netMessage;
        }

        public static BlockState PraseRecvMsg(NetMessage netMessage)
        {
            return netMessage.Param.value.AsSerializable<BlockState>();
        }
    }

    public static class Protocol_GetTransaction
    {
        public static NetMessage CreateSendMsg(UInt256 key, string id = "", UInt64 snapid = 0, bool useServerSnap = false)
        {
            Param param = new Param() { snapid = snapid,tableid = Prefixes.DATA_Transaction.ToBytes(), key = key.ToArray()};
            NetMessage netMessage = NetMessage.Create("_db.snapshot.getvalue", param, id);
            if (useServerSnap)
                netMessage = NetMessage.Create("_db.getvalue", param, id);
            return netMessage;
        }

        public static Transaction PraseRecvMsg(NetMessage netMessage)
        {

            return netMessage.Param.value.AsSerializable<TransactionState>().Transaction;
        }
    }
    */
}
