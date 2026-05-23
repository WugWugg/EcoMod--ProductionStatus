using Eco.Gameplay.Items;
using Eco.Shared.Time;
using Eco.Shared.Utils;
using Eco.Simulation.Time;
using Eco.Stats;
using Eco.World;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

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
        public Dictionary<int, ItemStatus> Items { get; set; } = new();

        public void Record(ItemStatusEvent e) =>
            GetOrCreate(e.EcoItemId).Record(e);

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
    }
    

    public sealed class ItemStatus
    {
        public int EcoItemId { get; set; }

        // Rolling 24hr buckets
        private HourBucket[] Buckets { get; set; } =
            Enumerable.Range(0, 24).Select(_ => new HourBucket(-1)).ToArray();

        private int CurrentWorldHour => (int)(WorldTime.Seconds / 3600);// cast to closest whole integer; no need for sub-second precision
        private int CurrentBucketIndex => CurrentWorldHour % 24;

        public long AllTimeProduced { get; set; }

        // UserId -> amount produced (in this run)
        public Dictionary<int, long> ProducerTotals { get; set; } = new();

        public long ProducedLast24h => Buckets.Sum(x => x.Produced);
        public long ConsumedLast24h => Buckets.Sum(x => x.Consumed);

        public ItemStatus(int id) { EcoItemId = id; }

        public void Record(ItemStatusEvent e)
        {
            ref var bucket = ref Buckets[CurrentBucketIndex];
            if (bucket.WorldHour != CurrentWorldHour)
                bucket = new HourBucket(CurrentWorldHour);
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
    }

    public sealed class HourBucket
    {
        public int WorldHour { get; }
        public long Produced {  get; set; }
        public long Consumed { get; set; }

        public decimal MedianPrice => WeightedMedianPrice();

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

        public decimal WeightedMedianPrice()
        {
            if (PriceSamples.Count == 0)
                return 0m;

            var sorted = PriceSamples
                .OrderBy(x => x.Key);

            var totalWeight = PriceSamples.Values.Sum();
            var halfway = totalWeight / 2;

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
}
