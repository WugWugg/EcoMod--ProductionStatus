using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace ProductionStatus
{
    public static class Storage
    {
        internal const int SAVE_VERSION = 1;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// Serialize to JSON for file storage.
        /// </summary>
        /// <param name="store">Store to serialize.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException">Throws if data is unable to be serialized.</exception>
        public static string ToJson(ProductionStatusStore store)
        {
            var payload = new ProductionStatusStoreDto(store);
            return JsonSerializer.Serialize(payload, Options);
        }

        /// <summary>
        /// Takes in file content as a string and deserializes them into the model.
        /// </summary>
        /// <param name="raw">Raw plaintext from file.</param>
        /// <returns>A hydrated <see cref="ProductionStatusStore"/></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="JsonException"></exception>
        /// <exception cref="NotSupportedException"></exception>
        public static ProductionStatusStore? FromJson(string rawJson)
        {
            return JsonSerializer.Deserialize<ProductionStatusStoreDto>(rawJson, Options)?.IntoModel();
        }
    }

    public sealed class ProductionStatusStoreDto
    {
        public int SaveVersion { get; set; } = 0;
        public ItemStatusDto[] Items { get; set; } = [];

        public ProductionStatusStoreDto() { }

        public ProductionStatusStoreDto(ProductionStatusStore store)
        {
            SaveVersion = Storage.SAVE_VERSION;
            Items = store.Items.Values
                .Select(x => new ItemStatusDto(x))
                .ToArray();
        }

        public ProductionStatusStore IntoModel()
        {
            var store = new ProductionStatusStore();
            foreach (var dtoItem in Items)
            {
                store.Items[dtoItem.Id] = dtoItem.IntoModel();
            }
            return store;
        }
    }



    public sealed class ItemStatusDto
    {
        public int Id { get; set; } = -1;
        public HourBucketDto[] Buckets { get; set; } = [];
        public int AllTimeProduced { get; set; } = -1;
        public Dictionary<int, int> ProducerTotals { get; set; } = new();

        public ItemStatusDto() { }

        public ItemStatusDto(ItemStatus status)
        {
            Id = status.EcoItemId;
            Buckets = status.Buckets.Select(x => new HourBucketDto(x)).ToArray();
            AllTimeProduced = status.AllTimeProduced;
            ProducerTotals = new Dictionary<int, int>(status.ProducerTotals);
        }

        public ItemStatusDto(int id, HourBucketDto[] buckets, int allTimeProduced, Dictionary<int, int> producerTotals)
        {
            Id = id;
            Buckets = buckets;
            AllTimeProduced = allTimeProduced;
            ProducerTotals = producerTotals;
        }

        public ItemStatus IntoModel()
        {
            var status = new ItemStatus(Id);
            status.Buckets = Buckets.Select(x => x.IntoModel()).ToArray();
            status.AllTimeProduced = AllTimeProduced;
            status.ProducerTotals = new Dictionary<int, int>(ProducerTotals);
            return status;
        }
    }

    public sealed class HourBucketDto
    {
        public int WorldHour { get; set; } = -1;
        public int Produced { get; set; } = -1;
        public int Consumed { get; set; } = -1;
        public Dictionary<decimal, int> PriceSamples { get; set; } = new();

        public HourBucketDto() { }

        public HourBucketDto(HourBucket bucket)
        {
            WorldHour = bucket.WorldHour;
            Produced = bucket.Produced;
            Consumed = bucket.Consumed;
            PriceSamples = new Dictionary<decimal, int>(bucket.PriceSamples);
        }

        public HourBucket IntoModel()
        {
            var hourBucket = new HourBucket(WorldHour);
            hourBucket.Produced = Produced;
            hourBucket.Consumed = Consumed;
            // No need for cloning as key and value are types that will be owned and not be referenced.
            hourBucket.PriceSamples = new Dictionary<decimal, int>(PriceSamples);
            return hourBucket;
        }
    }
}
