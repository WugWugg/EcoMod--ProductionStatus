using Eco.Core.Utils;
using Eco.Gameplay.Items;
using Eco.Shared.Serialization;
using Eco.Shared.Time;
using Eco.Shared.Utils;
using Eco.Simulation.Time;
using Eco.Stats;
using Eco.World;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace ProductionStatus
{
    public enum ItemStatusEventType
    {
        Produced,
        Consumed,
        Traded
    }

    public sealed record ItemStatusEvent(
        ItemStatusEventType Type,
        int EcoItemId,
        int Quantity,
        int? UserId = null,
        decimal? UnitPrice = null
    );

    public sealed class ProductionStatusStore
    {
        internal Dictionary<int, ItemStatus> Items { get; set; } = new();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        /// <param name="currentWorldHour">Age of the world in hours. <see cref="GetWorldHour"/></param>
        public void Record(ItemStatusEvent e, int currentWorldHour) =>
            GetOrCreate(e.EcoItemId).Record(e, currentWorldHour);

        public static int GetCurrentHour() => (int)(WorldTime.Seconds / 3600);

        private ItemStatus GetOrCreate(int id)
        {
            if (Items.TryGetValue(id, out var itemStatus)) 
                return itemStatus;
            else
            {
                var newItemStatus = new ItemStatus(id);
                Items.Add(id, newItemStatus);
                return newItemStatus;
            }
        }

        public bool TryGetItem(
            int ecoItemId,
            int currentWorldHour,
            [NotNullWhen(true)] out ItemStatusSnapshot? snapshot)
        {
            if (Items.TryGetValue(ecoItemId, out var status))
            {
                snapshot = status.BuildSnapshot(currentWorldHour);
                return true;
            } else
            {
                snapshot = null;
                return false;
            }
        }
    }
    
    public sealed class ItemStatusSnapshot
    {
        public required int EcoItemId { get; init; }
        public required int AllTimeProduced { get; init; }
        public required int ProducedLast24h { get; init; }
        public required int ConsumedLast24h { get; init; }
        public required decimal MedianPrice { get; init; }
        public required FrozenDictionary<int, int> ProducerTotals { get; init; }
    }

    public sealed class ItemStatus
    {
        public int EcoItemId { get; private set; }

        // Rolling 24hr buckets
        internal HourBucket[] Buckets { get; set; } =
            Enumerable.Range(0, 24).Select(_ => new HourBucket(-1)).ToArray();

        internal int AllTimeProduced { get; set; }

        // UserId -> amount produced (in this run)
        internal Dictionary<int, int> ProducerTotals { get; set; } = new();

        public ItemStatus(int id) { EcoItemId = id; }

        public void Record(ItemStatusEvent e, int currentWorldHour)
        {
            int currentBucketIndex = currentWorldHour % 24;
            ref var bucket = ref Buckets[currentBucketIndex];
            if (bucket.WorldHour != currentWorldHour)
                bucket = new HourBucket(currentWorldHour);
            bucket.Record(e);
            switch (e.Type)
            {
                case ItemStatusEventType.Produced:
                    AllTimeProduced += e.Quantity;

                    if (e.UserId is int userId)
                        ProducerTotals[userId] =
                            ProducerTotals.GetValueOrDefault(userId) + e.Quantity;
                    break;
            }
        }

        public ItemStatusSnapshot BuildSnapshot(int currentWorldHour)
        {
            var freshBuckets = Buckets.Where(x => IsFresh(x, currentWorldHour));
            int producedLast24h = freshBuckets.Sum(x => x.Produced);
            int consumedLast24h = freshBuckets.Sum(x => x.Consumed);
            Dictionary<decimal, int> priceSamples = new();
            foreach (var bucket in freshBuckets)
            {
                foreach (var (key, value) in bucket.PriceSamples)
                {
                    priceSamples[key] =
                        priceSamples.GetValueOrDefault(key) + value;
                }
            }
            return new ItemStatusSnapshot {
                EcoItemId = EcoItemId,
                AllTimeProduced = AllTimeProduced,
                ProducedLast24h = producedLast24h,
                ConsumedLast24h = consumedLast24h,
                MedianPrice = WeightedMedianPrice(priceSamples),
                ProducerTotals = ProducerTotals.ToFrozenDictionary()
            };
        }

        private static bool IsFresh(HourBucket bucket,  int currentWorldHour)
        {
            return bucket.WorldHour > currentWorldHour - 24
                && bucket.WorldHour <= currentWorldHour;
        }

        private static decimal WeightedMedianPrice(Dictionary<decimal, int> priceSamples)
        {
            if (priceSamples.Count == 0)
                return 0m;
            var sorted = priceSamples.OrderBy(x => x.Key);
            double halfway = priceSamples.Values.Sum() / 2.0;
            int cumulative = 0;
            foreach (var (price, quantity) in sorted)
            {
                cumulative += quantity;

                if (cumulative >= halfway)
                    return price;
            }
            return sorted.Last().Key;
        }
    }

    public sealed class HourBucket
    {
        public int WorldHour { get; }
        public int Produced {  get; set; }
        public int Consumed { get; set; }

        // UnitPrice -> Count
        public Dictionary<decimal, int> PriceSamples { get; set; } = new();

        public HourBucket(int worldHour) { WorldHour = worldHour; }

        public void Record(ItemStatusEvent e)
        {
            switch (e.Type)
            {
                case ItemStatusEventType.Produced:
                    Produced += e.Quantity;
                    break;

                case ItemStatusEventType.Consumed:
                    Consumed += e.Quantity;
                    break;

                case ItemStatusEventType.Traded:
                    if (e.UnitPrice is decimal price)
                        PriceSamples[price] = 
                            PriceSamples.GetValueOrDefault(price) + e.Quantity;
                    break;
            }
        }
    }
}
