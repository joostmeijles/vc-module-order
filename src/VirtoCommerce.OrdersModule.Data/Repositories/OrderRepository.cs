using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Data.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;

namespace VirtoCommerce.OrdersModule.Data.Repositories
{
    public class OrderRepository : DbContextRepositoryBase<OrderDbContext>, IOrderRepository
    {
        public OrderRepository(OrderDbContext dbContext, IUnitOfWork unitOfWork = null) : base(dbContext, unitOfWork)
        {
        }

        public IQueryable<CustomerOrderEntity> CustomerOrders => DbContext.Set<CustomerOrderEntity>();
        public IQueryable<ShipmentEntity> Shipments => DbContext.Set<ShipmentEntity>();
        public IQueryable<ShipmentPackageEntity> ShipmentPackagesPackages => DbContext.Set<ShipmentPackageEntity>();
        public IQueryable<ShipmentItemEntity> ShipmentItems => DbContext.Set<ShipmentItemEntity>();
        public IQueryable<DiscountEntity> Discounts => DbContext.Set<DiscountEntity>();
        public IQueryable<TaxDetailEntity> TaxDetails => DbContext.Set<TaxDetailEntity>();
        public IQueryable<PaymentInEntity> InPayments => DbContext.Set<PaymentInEntity>();
        public IQueryable<AddressEntity> Addresses => DbContext.Set<AddressEntity>();
        public IQueryable<LineItemEntity> LineItems => DbContext.Set<LineItemEntity>();
        public IQueryable<PaymentGatewayTransactionEntity> Transactions => DbContext.Set<PaymentGatewayTransactionEntity>();
        public IQueryable<OrderDynamicPropertyObjectValueEntity> OrderDynamicPropertyObjectValues => DbContext.Set<OrderDynamicPropertyObjectValueEntity>();

        public virtual async Task<CustomerOrderEntity[]> GetCustomerOrdersByIdsAsync(string[] ids, string responseGroup = null)
        {
            var customerOrderResponseGroup = EnumUtility.SafeParseFlags(responseGroup, CustomerOrderResponseGroup.Full);

            var result = await CustomerOrders.Where(x => ids.Contains(x.Id))
                                             .Include(x=> x.Discounts)
                                             .Include(x=> x.TaxDetails).ToArrayAsync();

            var breakingLoadTasks = new List<Task>();
            if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithAddresses))
            {
                breakingLoadTasks.Add(Addresses.Where(x => ids.Contains(x.CustomerOrderId)).LoadAsync());
            }

            if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithInPayments))
            {
                var shipmentsLoadBreakingQuery = InPayments.Where(x => ids.Contains(x.CustomerOrderId))
                                                 .Include(x => x.Discounts)
                                                 .Include(x => x.TaxDetails)
                                                 .Include(x => x.Addresses)
                                                 .Include(x => x.Transactions);
                if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithDynamicProperties))
                {
                    shipmentsLoadBreakingQuery.Include(x => x.DynamicPropertyObjectValues);
                }
                breakingLoadTasks.Add(shipmentsLoadBreakingQuery.LoadAsync());
            }

            if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithItems))
            {
                var itemsLoadBreakingQuery = LineItems.Where(x => ids.Contains(x.CustomerOrderId))
                                                .Include(x => x.Discounts)
                                                .Include(x => x.TaxDetails)
                                                .Include(x => x.DynamicPropertyObjectValues);
                if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithDynamicProperties))
                {
                    itemsLoadBreakingQuery.Include(x => x.DynamicPropertyObjectValues);
                }
                breakingLoadTasks.Add(itemsLoadBreakingQuery.LoadAsync());
            }

            if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithShipments))
            {
                var shipmentLoadBreakingQuery = Shipments.Where(x => ids.Contains(x.CustomerOrderId))
                                               .Include(x => x.Discounts)
                                               .Include(x => x.TaxDetails)
                                               .Include(x => x.Addresses)
                                               .Include(x => x.Items)
                                               .Include(x => x.Packages);

                if (customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithDynamicProperties))
                {
                    shipmentLoadBreakingQuery.Include(x => x.DynamicPropertyObjectValues);
                }
                breakingLoadTasks.Add(shipmentLoadBreakingQuery.LoadAsync());
            }

            await Task.WhenAll(breakingLoadTasks);

            if (!customerOrderResponseGroup.HasFlag(CustomerOrderResponseGroup.WithPrices))
            {
                foreach (var customerOrder in result)
                {
                    customerOrder.ResetPrices();
                }
            }

            return result;
        }

        public virtual async Task RemoveOrdersByIdsAsync(string[] ids)
        {
            var orders = await GetCustomerOrdersByIdsAsync(ids);
            foreach (var order in orders)
            {
                Remove(order);
            }
        }
    }
}
