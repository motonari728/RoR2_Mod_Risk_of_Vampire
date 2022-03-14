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
        float possessedItemChance;
        float ospPercent;
        float invTime;
        float moneyScaling;
        float healPerSecond;

        public SyncConfig()
        {
        }

        public SyncConfig(float possessedItemChance, float ospPercent, float invTime, float moneyScaling, float healPerSecond)
        {
            this.possessedItemChance = possessedItemChance;
            this.ospPercent = ospPercent;
            this.invTime = invTime;
            this.moneyScaling = moneyScaling;
            this.healPerSecond = healPerSecond;
        }

        public void Deserialize(NetworkReader reader)
        {
            possessedItemChance = reader.ReadSingle();
            ospPercent = reader.ReadSingle();
            invTime = reader.ReadSingle();
            moneyScaling = reader.ReadSingle();
            healPerSecond = reader.ReadSingle();
        }

        public void OnReceived()
        {
            if  (NetworkServer.active)
            {
                return;
            }

            RiskOfVampire.possessedItemChance = possessedItemChance;
            RiskOfVampire.ospPercent = ospPercent;
            RiskOfVampire.invTime = invTime;
            RiskOfVampire.moneyScaling = moneyScaling;
            RiskOfVampire.healPerSecond = healPerSecond;

            Debug.Log("SyncConfig OnReceived");
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.Write(possessedItemChance);
            writer.Write(ospPercent);
            writer.Write(invTime);
            writer.Write(moneyScaling);
            writer.Write(healPerSecond);
        }
    }
}
