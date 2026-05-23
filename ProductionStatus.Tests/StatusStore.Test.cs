using Eco.Gameplay.Players;
using System;
using System.Collections.Generic;
using System.Text;

namespace ProductionStatus
{
    [TestClass]
    public sealed class StatusStore_TestSuite
    {
        [TestMethod]
        public void produced_items_increase_hourly_totals()
        {
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                0);
            Assert.IsTrue(status.TryGetItem(1, 0, out var item));
            Assert.AreEqual(50, item.ProducedLast24h);   
        }

        [TestMethod]
        public void consumed_items_increases_hourly_totals()
        {
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Consumed,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                0);
            Assert.IsTrue(status.TryGetItem(1, 0, out var item));
            Assert.AreEqual(50, item.ConsumedLast24h);
        }

        [TestMethod]
        public void reused_bucket_slot_does_not_keep_old_data()
        {
            var status = new ProductionStatusStore();
            status.Record(new ItemStatusEvent(
                ItemStatusEventType.Produced,
                EcoItemId: 1,
                Quantity: 50,
                UserId: 111),
                currentWorldHour: 0);
            status.Record(new ItemStatusEvent(
                ItemStatusEventType.Produced,
                EcoItemId: 1,
                Quantity: 25,
                UserId: 111),
                currentWorldHour: 24);

            Assert.IsTrue(status.TryGetItem(1, 24, out var item));
            Assert.AreEqual(25, item.ProducedLast24h);
        }

        [TestMethod]
        public void old_buckets_expire_after_24_hours() 
        {
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Consumed,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 50,
                    UnitPrice: 1),
                0);
            { // Look at the last hour before expiration -- hour 23
                var currentHour = 23;
                Assert.IsTrue(status.TryGetItem(1, currentHour, out var item)); 
                Assert.AreEqual(50, item.ProducedLast24h);
                Assert.AreEqual(50, item.ConsumedLast24h);
                Assert.AreEqual(1, item.MedianPrice);
            }
            { // Look this at the hour of rollover -- hour 24
                var currentHour = 24;
                Assert.IsTrue(status.TryGetItem(1, currentHour, out var item)); 
                Assert.AreEqual(0, item.ProducedLast24h);
                Assert.AreEqual(0, item.ConsumedLast24h);
                Assert.AreEqual(0, item.MedianPrice);
            }
        }

        [TestMethod]
        public void production_increases_all_time_total() 
        {
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 25,
                    UserId: 111),
                24);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 10,
                    UserId: 111),
                48);
            Assert.IsTrue(status.TryGetItem(1, 48, out var item));
            Assert.AreEqual(85, item.AllTimeProduced);
        }

        [TestMethod]
        public void production_increases_contributor_total() 
        {
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 25,
                    UserId: 111),
                24);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 10,
                    UserId: 111),
                48);
            Assert.IsTrue(status.TryGetItem(1, 48, out var item));
            Assert.IsTrue(item.ProducerTotals.TryGetValue(111, out var count));
            Assert.AreEqual(85, count);
        }

        [TestMethod]
        public void weighted_median_returns_expected_price() 
        {
            // $1 -- 5
            // $10 -- 1 (expected)
            // $100 -- 2
            // $1000 -- 3
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 5,
                    UserId: 111,
                    UnitPrice: 1),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 1,
                    UserId: 111,
                    UnitPrice: 10),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 2,
                    UserId: 111,
                    UnitPrice: 100),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 3,
                    UserId: 111,
                    UnitPrice: 1000),
                0);
            Assert.IsTrue(status.TryGetItem(1, 0, out var item));
            Assert.AreEqual(10, item.MedianPrice);
        }

        [TestMethod]
        public void weighted_median_ignores_outlier_trades() 
        {
            // $1 -- 100 (expected)
            // $9999 -- 1
            var status = new ProductionStatusStore();
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 100,
                    UserId: 111,
                    UnitPrice: 1),
                0);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 1,
                    UserId: 111,
                    UnitPrice: 9999),
                0);
            Assert.IsTrue(status.TryGetItem(1, 0, out var item));
            Assert.AreEqual(1, item.MedianPrice);
        }

        [TestMethod]
        public void save_load_preserves_state() 
        {
            var status = new ProductionStatusStore();
            var currentHour = 0;
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                currentHour);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Consumed,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                currentHour);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 50,
                    UnitPrice: 1),
                currentHour);
            // next day
            currentHour = 24;
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Produced,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                currentHour);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Consumed,
                    EcoItemId: 1,
                    Quantity: 50,
                    UserId: 111),
                currentHour);
            status.Record(
                new ItemStatusEvent(
                    ItemStatusEventType.Traded,
                    EcoItemId: 1,
                    Quantity: 50,
                    UnitPrice: 1),
                currentHour);
            string saveString = Storage.ToJson(status);
            var readInStatus = Storage.FromJson(saveString);
            Assert.IsNotNull(readInStatus);

            Assert.IsTrue(readInStatus.TryGetItem(1, currentHour, out var item));
            Assert.AreEqual(50, item.ProducedLast24h);
            Assert.AreEqual(50, item.ConsumedLast24h);
            Assert.AreEqual(1, item.MedianPrice);

            Assert.AreEqual(100, item.AllTimeProduced);
            Assert.IsTrue(item.ProducerTotals.TryGetValue(111, out var count));
            Assert.AreEqual(100, count);
        }
    }    
}
