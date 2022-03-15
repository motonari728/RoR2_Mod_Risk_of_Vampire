using System;
using System.Collections.Generic;
using System.Text;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;
using UnityEngine;

namespace RiskOfVampire
{
    internal class SyncConfig : INetMessage
    {
        public float possessedItemChance;
        public float ospPercent;
        public float invTime;
        public float moneyScaling;
        public float healPerSecond;
        public int itemPickerOptionAmount;
        public int whiteItemUpperLimit;
        public int greenItemUpperLimit;

        public SyncConfig()
        {
        }

        public SyncConfig(float possessedItemChance, float ospPercent, float invTime, float moneyScaling, float healPerSecond, int itemPickerOptionAmount, int whiteItemUpperLimit, int greenItemUpperLimit)
        {
            this.possessedItemChance = possessedItemChance;
            this.ospPercent = ospPercent;
            this.invTime = invTime;
            this.moneyScaling = moneyScaling;
            this.healPerSecond = healPerSecond;
            this.itemPickerOptionAmount = itemPickerOptionAmount;
            this.whiteItemUpperLimit = whiteItemUpperLimit;
            this.greenItemUpperLimit = greenItemUpperLimit;
        }

        public void Deserialize(NetworkReader reader)
        {
            possessedItemChance = reader.ReadSingle();
            ospPercent = reader.ReadSingle();
            invTime = reader.ReadSingle();
            moneyScaling = reader.ReadSingle();
            healPerSecond = reader.ReadSingle();
            itemPickerOptionAmount = reader.ReadInt32();
            whiteItemUpperLimit = reader.ReadInt32();
            greenItemUpperLimit = reader.ReadInt32();
        }

        public void OnReceived()
        {
            if  (NetworkServer.active)
            {
                return;
            }

            // hostでない場合、RoVクラスでインスタンス化しない
            // ここでRoVクラスからの参照を設定する
            RiskOfVampire.syncConfig = this;
            Debug.Log("SyncConfig OnReceived");
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(possessedItemChance);
            writer.Write(ospPercent);
            writer.Write(invTime);
            writer.Write(moneyScaling);
            writer.Write(healPerSecond);
            writer.Write(itemPickerOptionAmount);
            writer.Write(whiteItemUpperLimit);
            writer.Write(greenItemUpperLimit);
        }
    }
}
