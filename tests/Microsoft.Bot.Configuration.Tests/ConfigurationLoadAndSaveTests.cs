﻿using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace Microsoft.Bot.Configuration.Tests
{
    [TestClass]
    public class ConfingurationLoadAndSaveTests
    {
        [TestMethod]
        public async Task DeserializeBotFile()
        {
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            Assert.AreEqual("test", config.Name);
            Assert.AreEqual("test description", config.Description);
            Assert.AreEqual("", config.SecretKey);
            Assert.AreEqual(10, config.Services.Count);

            // verify types are right
            foreach (var service in config.Services)
            {
                switch (service.Type)
                {
                    case ServiceTypes.AppInsights:
                        Assert.AreEqual(typeof(AppInsightsService), service.GetType());
                        break;
                    case ServiceTypes.Bot:
                        Assert.AreEqual(typeof(BotService), service.GetType());
                        break;
                    case ServiceTypes.BlobStorage:
                        Assert.AreEqual(typeof(BlobStorageService), service.GetType());
                        break;
                    case ServiceTypes.CosmosDB:
                        Assert.AreEqual(typeof(CosmosDbService), service.GetType());
                        break;
                    case ServiceTypes.Generic:
                        Assert.AreEqual(typeof(GenericService), service.GetType());
                        break;
                    case ServiceTypes.Dispatch:
                        Assert.AreEqual(typeof(DispatchService), service.GetType());
                        break;
                    case ServiceTypes.Endpoint:
                        Assert.AreEqual(typeof(EndpointService), service.GetType());
                        break;
                    case ServiceTypes.File:
                        Assert.AreEqual(typeof(FileService), service.GetType());
                        break;
                    case ServiceTypes.Luis:
                        Assert.AreEqual(typeof(LuisService), service.GetType());
                        break;
                    case ServiceTypes.QnA:
                        Assert.AreEqual(typeof(QnAMakerService), service.GetType());
                        break;
                    default:
                        throw new Exception("Unknown service type!");
                }
            }
        }


        [TestMethod]
        public async Task LoadAndSaveUnencryptedBotFile()
        {
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            await config.SaveAsAsync("save.bot");

            var config2 = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            Assert.AreEqual(JsonConvert.SerializeObject(config2), JsonConvert.SerializeObject(config), "saved should be the same");
        }

        [TestMethod]
        public async Task CantLoadWithoutSecret()
        {
            string secret = BotConfiguration.GenerateKey();
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            await config.SaveAsAsync("save.bot", secret);

            try
            {
                await BotConfiguration.LoadAsync(@"save.bot");
                Assert.Fail("Load should have thrown due to no secret");
            }
            catch { }
        }

        [TestMethod]
        public async Task LoadFromFolderWithSecret()
        {
            string secret = BotConfiguration.GenerateKey();
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            await config.SaveAsAsync("save.bot", secret);
            await BotConfiguration.LoadFromFolderAsync(".", secret);
        }

        [TestMethod][ExpectedException(typeof(System.Exception))]
        public async Task FailLoadFromFolderWithNoSecret()
        {
            string secret = BotConfiguration.GenerateKey();
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            await config.SaveAsAsync("save.bot", secret);
            await BotConfiguration.LoadFromFolderAsync(".");
        }

        [TestMethod]
        public async Task LoadFromFolderNoSecret()
        {
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            await config.SaveAsAsync("save.bot");
            await BotConfiguration.LoadFromFolderAsync(".");
        }

        [TestMethod][ExpectedException(typeof(System.IO.FileNotFoundException))]
        public async Task LoadNotExistentFile()
        {
            var config = await BotConfiguration.LoadAsync(@"..\..\filedoesntexist.bot");
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentNullException))]
        public async Task NullFile()
        {
            var config = await BotConfiguration.LoadAsync(null);
        }

        [TestMethod]
        [ExpectedException(typeof(System.IO.DirectoryNotFoundException))]
        public async Task LoadNotExistentFolder()
        {
            var config = await BotConfiguration.LoadFromFolderAsync(@"\prettysurethisdoesnotexist");
        }

        [TestMethod]
        [ExpectedException(typeof(System.ArgumentNullException))]
        public async Task NullFolder()
        {
            var config = await BotConfiguration.LoadFromFolderAsync(null);
        }

        [TestMethod]
        public async Task CantSaveWithoutSecret()
        {
            string secret = BotConfiguration.GenerateKey();
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            await config.SaveAsAsync("save.bot", secret);

            var config2 = await BotConfiguration.LoadAsync(@"save.bot", secret);
            try
            {
                await config2.SaveAsAsync("save.bot");
                Assert.Fail("Save() should have thrown due to no secret");
            }
            catch { }
            config2.ClearSecret();
            await config2.SaveAsAsync("save.bot", secret);
        }

        [TestMethod]
        public async Task LoadAndSaveEncrypted()
        {
            string secret = BotConfiguration.GenerateKey();
            var config = await BotConfiguration.LoadAsync(@"..\..\test.bot");
            Assert.AreEqual("", config.SecretKey, "There should be no secretKey");

            // save with secret
            await config.SaveAsAsync("savesecret.bot", secret);
            Assert.IsTrue(config.SecretKey?.Length > 0, "There should be a secretKey");

            // load with secret
            var config2 = await BotConfiguration.LoadAsync("savesecret.bot", secret);
            Assert.IsTrue(config2.SecretKey?.Length > 0, "There should be a secretKey");
            Assert.AreEqual(config.SecretKey, config2.SecretKey, "SecretKeys should be the same");

            // make sure these were decrypted
            for (int i = 0; i < config.Services.Count; i++)
            {
                Assert.AreEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));

                switch (config.Services[i].Type)
                {
                    case ServiceTypes.Bot:
                        break;

                    case ServiceTypes.AppInsights:
                        {
                            var appInsights = (AppInsightsService)config2.Services[i];
                            Assert.IsTrue(appInsights.InstrumentationKey.Contains("0000"), "failed to decrypt instrumentationKey");
                            Assert.AreEqual(appInsights.ApplicationId, "00000000-0000-0000-0000-000000000007", "failed to decrypt applicationId");
                            Assert.AreEqual(appInsights.ApiKeys["key1"], "testKey1", "failed to decrypt key1");
                            Assert.AreEqual(appInsights.ApiKeys["key2"], "testKey2", "failed to decrypt key2");
                        }
                        break;

                    case ServiceTypes.BlobStorage:
                        {
                            var blobStorage = (BlobStorageService)config2.Services[i];
                            Assert.AreEqual("UseDevelopmentStorage=true;", blobStorage.ConnectionString, "failed to decrypt connectionString");
                            Assert.AreEqual("testContainer", blobStorage.Container, "failed to decrypt Container");
                        }
                        break;

                    case ServiceTypes.CosmosDB:
                        {
                            var cosmosDb = (CosmosDbService)config2.Services[i];
                            Assert.AreEqual("UseDevelopmentStorage=true;", cosmosDb.ConnectionString, "failed to decrypt connectionString");
                            Assert.AreEqual("testDatabase", cosmosDb.Database, "failed to decrypt database");
                            Assert.AreEqual("testCollection", cosmosDb.Collection, "failed to decrypt collection");

                        }
                        break;

                    case ServiceTypes.Dispatch:
                        {
                            var dispatch = (DispatchService)config2.Services[i];
                            Assert.IsTrue(dispatch.AuthoringKey.Contains("0000"), "failed to decrypt authoringkey");
                            Assert.IsTrue(dispatch.SubscriptionKey.Contains("0000"), "failed to decrypt subscriptionKey");
                        }
                        break;

                    case ServiceTypes.Endpoint:
                        {
                            var endpoint = (EndpointService)config2.Services[i];
                            Assert.AreEqual("testpassword", endpoint.AppPassword, "failed to decrypt appPassword");
                        }
                        break;

                    case ServiceTypes.File:
                        break;

                    case ServiceTypes.Luis:
                        {
                            var luis = (LuisService)config2.Services[i];
                            Assert.IsTrue(luis.AuthoringKey.Contains("0000"), "failed to decrypt authoringkey");
                            Assert.IsTrue(luis.SubscriptionKey.Contains("0000"), "failed to decrypt subscriptionKey");
                        }
                        break;

                    case ServiceTypes.QnA:
                        {
                            var qna = (QnAMakerService)config2.Services[i];
                            Assert.IsTrue(qna.KbId.Contains("0000"), "kbId should not be changed");
                            Assert.IsTrue(qna.EndpointKey.Contains("0000"), "failed to decrypt EndpointKey");
                            Assert.IsTrue(qna.SubscriptionKey.Contains("0000"), "failed to decrypt SubscriptionKey");
                        }
                        break;

                    case ServiceTypes.Generic:
                        {
                            var generic = (GenericService)config2.Services[i];
                            Assert.AreEqual(generic.Url, "https://bing.com", "url should not change");
                            Assert.AreEqual(generic.Configuration["key1"], "testKey1", "failed to decrypt key1");
                            Assert.AreEqual(generic.Configuration["key2"], "testKey2", "failed to decrypt key2");
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unknown service type {config.Services[i].Type}");
                }
            }

            // encrypt in memory copy
            config2.Encrypt(secret);

            // make sure these are all true
            for (int i = 0; i < config.Services.Count; i++)
            {
                switch (config.Services[i].Type)
                {
                    case ServiceTypes.AppInsights:
                        {
                            var appInsights = (AppInsightsService)config2.Services[i];
                            Assert.IsFalse(appInsights.InstrumentationKey.Contains("0000"), "failed to encrypt instrumentationKey");
                            Assert.AreEqual(appInsights.ApplicationId, "00000000-0000-0000-0000-000000000007", "should not encrypt applicationId");
                            Assert.AreNotEqual(appInsights.ApiKeys["key1"], "testKey1", "failed to encrypt key1");
                            Assert.AreNotEqual(appInsights.ApiKeys["key2"], "testKey2", "failed to encrypt key2");
                        }
                        break;

                    case ServiceTypes.BlobStorage:
                        {
                            var azureStorage = (BlobStorageService)config2.Services[i];
                            Assert.AreNotEqual("UseDevelopmentStorage=true;", azureStorage.ConnectionString, "failed to encrypt connectionString");
                            Assert.AreEqual("testContainer", azureStorage.Container, "should not change container");
                        }
                        break;

                    case ServiceTypes.CosmosDB:
                        {
                            var cosmosdb = (CosmosDbService)config2.Services[i];
                            Assert.AreNotEqual("UseDevelopmentStorage=true;", cosmosdb.ConnectionString, "failed to encrypt connectionString");
                            Assert.AreEqual("testDatabase", cosmosdb.Database, "should not change database");
                            Assert.AreEqual("testCollection", cosmosdb.Collection, "should not change collection");
                        }
                        break;

                    case ServiceTypes.Bot:
                        Assert.AreEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));
                        break;

                    case ServiceTypes.Dispatch:
                        {
                            Assert.AreNotEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));
                            var dispatch = (DispatchService)config2.Services[i];
                            Assert.IsFalse(dispatch.AuthoringKey.Contains("0000"), "failed to encrypt authoringkey");
                            Assert.IsFalse(dispatch.SubscriptionKey.Contains("0000"), "failed to encrypt subscriptionKey");
                        }
                        break;

                    case ServiceTypes.Endpoint:
                        {
                            Assert.AreNotEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));
                            var endpoint = (EndpointService)config2.Services[i];
                            Assert.IsTrue(endpoint.AppId.Contains("0000"), "appId should not be changed");
                            Assert.IsFalse(endpoint.AppPassword.Contains("testpassword"), "failed to encrypt appPassword");
                        }
                        break;

                    case ServiceTypes.File:
                        Assert.AreEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));
                        break;

                    case ServiceTypes.Luis:
                        {
                            Assert.AreNotEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));
                            var luis = (LuisService)config2.Services[i];
                            Assert.IsFalse(luis.AuthoringKey.Contains("0000"), "failed to encrypt authoringkey");
                            Assert.IsFalse(luis.SubscriptionKey.Contains("0000"), "failed to encrypt subscriptionKey");
                        }
                        break;

                    case ServiceTypes.QnA:
                        {
                            Assert.AreNotEqual(JsonConvert.SerializeObject(config.Services[i]), JsonConvert.SerializeObject(config2.Services[i]));
                            var qna = (QnAMakerService)config2.Services[i];
                            Assert.IsTrue(qna.KbId.Contains("0000"), "kbId should not be changed");
                            Assert.IsFalse(qna.EndpointKey.Contains("0000"), "failed to encrypt EndpointKey");
                            Assert.IsFalse(qna.SubscriptionKey.Contains("0000"), "failed to encrypt SubscriptionKey");
                        }
                        break;
                    case ServiceTypes.Generic:
                        {
                            var generic = (GenericService)config2.Services[i];
                            Assert.AreEqual(generic.Url, "https://bing.com", "url should not change");
                            Assert.AreNotEqual(generic.Configuration["key1"], "testKey1", "failed to encrypt key1");
                            Assert.AreNotEqual(generic.Configuration["key2"], "testKey2", "failed to encrypt key2");
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unknown service type {config.Services[i].Type}");
                }
            }
        }
    }
}
