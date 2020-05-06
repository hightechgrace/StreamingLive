﻿using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Amazon.ApiGatewayManagementApi;
using Amazon.ApiGatewayManagementApi.Model;
using Amazon.DynamoDBv2.DocumentModel;

namespace StreamingLiveLambda
{
    public class Catchup
    {

        internal static void Store(string room, JObject message)
        {
            JArray messages = GetCatchup(room);
            
            //this just needs to run periodically.  
            if (messages.Count==0)
            {
                Connection.Cleanup();
                Catchup.Cleanup();
            }
            messages.Add(message);

            AmazonDynamoDBClient client = new AmazonDynamoDBClient();
            Table table = Table.LoadTable(client, "catchup");
            Document doc = new Document();
            doc["room"] = room;
            doc["messages"] = messages.ToString(Newtonsoft.Json.Formatting.None);
            doc["ts"] = DateTime.Now;
            table.PutItemAsync(doc);
        }

        internal static JArray GetCatchup(string room)
        {
            AmazonDynamoDBClient client = new AmazonDynamoDBClient();
            JArray result = new JArray();
            QueryRequest request = new QueryRequest
            {
                TableName = "catchup",
                KeyConditionExpression = "room = :room",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":room", new AttributeValue { S = room } } },
                ProjectionExpression = "messages",
            };
            QueryResponse response = client.QueryAsync(request).Result;
            if (response.Items.Count > 0) result = JArray.Parse(response.Items[0]["messages"].S);
            
            double threshold = DateTime.UtcNow.AddMinutes(-5).Ticks;
            for (int i = result.Count - 1; i >= 0; i--)
            {
                if (i > 19) result.RemoveAt(i);
                else
                {
                    try
                    {
                        if (Convert.ToDouble(result[i]["ts"]) < threshold) result.RemoveAt(i);
                    }
                    catch { }
                }
            }

            return result;
        }

        internal static void SendCatchup(string serviceUrl, string connectionId, string room, JArray messages)
        {
            JObject message = new JObject() { { "action", "catchup" }, { "room", room }, { "messages", messages } };
            MemoryStream stream = new MemoryStream(UTF8Encoding.UTF8.GetBytes(message.ToString(Newtonsoft.Json.Formatting.None)));
            AmazonApiGatewayManagementApiConfig config = new AmazonApiGatewayManagementApiConfig() { ServiceURL = serviceUrl };
            AmazonApiGatewayManagementApiClient client = new AmazonApiGatewayManagementApiClient(config);
            PostToConnectionRequest postReq = new PostToConnectionRequest() { ConnectionId = connectionId, Data = stream };
            client.PostToConnectionAsync(postReq);
        }

        internal static void Cleanup()
        {
            AmazonDynamoDBClient client = new AmazonDynamoDBClient();
            DateTime threshold = DateTime.UtcNow.AddMinutes(-30);
            ScanRequest request = new ScanRequest
            {
                TableName = "catchup",
                FilterExpression = "ts < :ts",
                ExpressionAttributeValues = new Dictionary<string, AttributeValue> { { ":ts", new AttributeValue { N = threshold.Ticks.ToString() } } },
                ProjectionExpression = "room, connectionId"
            };
            ScanResponse response = client.ScanAsync(request).Result;
            Table catchupTable = Table.LoadTable(client, "catchup");
            foreach (Dictionary<string, AttributeValue> item in response.Items)
            {
                Document doc = new Document();
                doc["room"] = item["room"].ToString();
                catchupTable.DeleteItemAsync(doc);
            }
        }


    }
}