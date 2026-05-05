using Microsoft.EntityFrameworkCore;
using POS_System.Data.Database;

namespace POS_System.IntegrationTests.Infrastructure;

internal static class DbCleaner
{
    /// <summary>
    /// Deletes all test-created data in dependency order, preserving seeded employees.
    /// </summary>
    internal static async Task CleanAllAsync(ApplicationDbContext db)
    {
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.ServiceOnTaxes.ExecuteDeleteAsync();
        await db.ServiceOnItemDiscounts.ExecuteDeleteAsync();
        await db.EmployeeOnServices.ExecuteDeleteAsync();
        await db.Services.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.Taxes.ExecuteDeleteAsync();
        await db.ItemDiscounts.ExecuteDeleteAsync();
        await db.TimeSlots.ExecuteDeleteAsync();
        await db.Carts.ExecuteUpdateAsync(s => s.SetProperty(c => c.CartDiscountId, (string?)null));
        await db.Carts.ExecuteDeleteAsync();
        await db.CardDiscounts.ExecuteDeleteAsync();
        await db.GiftCards.ExecuteDeleteAsync();
    }
}
