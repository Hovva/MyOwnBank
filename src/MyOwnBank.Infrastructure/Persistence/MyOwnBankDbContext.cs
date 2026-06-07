using Microsoft.EntityFrameworkCore;
using MyOwnBank.Infrastructure.Persistence.Entities;

namespace MyOwnBank.Infrastructure.Persistence;

public sealed class MyOwnBankDbContext(DbContextOptions<MyOwnBankDbContext> options) : DbContext(options)
{
    public DbSet<BankEntity> Banks => Set<BankEntity>();

    public DbSet<InvitationEntity> Invitations => Set<InvitationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BankEntity>(entity =>
        {
            entity.ToTable("banks");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(128);
            entity.HasMany(item => item.Currencies).WithOne().HasForeignKey(item => item.BankId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Members).WithOne().HasForeignKey(item => item.BankId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Cards).WithOne().HasForeignKey(item => item.BankId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(item => item.Shop).WithOne().HasForeignKey<ShopEntity>(item => item.BankId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(item => item.Transactions).WithOne().HasForeignKey(item => item.BankId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BankCurrencyEntity>(entity =>
        {
            entity.ToTable("bank_currencies");
            entity.HasKey(item => new { item.BankId, item.Code });
            entity.Property(item => item.Code).HasMaxLength(32);
            entity.Property(item => item.Name).HasMaxLength(64);
            entity.Property(item => item.Icon).HasMaxLength(128);
        });

        modelBuilder.Entity<BankMemberEntity>(entity =>
        {
            entity.ToTable("bank_members");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.BankId, item.TelegramUserId }).IsUnique();
            entity.Property(item => item.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<BankCardEntity>(entity =>
        {
            entity.ToTable("bank_cards");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CardNumber).HasMaxLength(32);
            entity.Property(item => item.HolderName).HasMaxLength(64);
            entity.HasMany(item => item.Balances).WithOne().HasForeignKey(item => item.CardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CardBalanceEntity>(entity =>
        {
            entity.ToTable("card_balances");
            entity.HasKey(item => new { item.CardId, item.CurrencyCode });
            entity.Property(item => item.CurrencyCode).HasMaxLength(32);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<ShopEntity>(entity =>
        {
            entity.ToTable("shops");
            entity.HasKey(item => item.BankId);
            entity.HasMany(item => item.Products).WithOne().HasForeignKey(item => item.BankId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShopProductEntity>(entity =>
        {
            entity.ToTable("shop_products");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(160);
            entity.Property(item => item.Description).HasMaxLength(512);
            entity.Property(item => item.CurrencyCode).HasMaxLength(32);
            entity.Property(item => item.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<BankTransactionEntity>(entity =>
        {
            entity.ToTable("bank_transactions");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.BankId, item.OccurredAt });
            entity.Property(item => item.Type).HasMaxLength(64);
            entity.Property(item => item.CurrencyCode).HasMaxLength(32);
            entity.Property(item => item.Amount).HasPrecision(18, 2);
            entity.Property(item => item.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<InvitationEntity>(entity =>
        {
            entity.ToTable("bank_invitations");
            entity.HasKey(item => item.Code);
            entity.Property(item => item.Code).HasMaxLength(16);
            entity.HasIndex(item => item.BankId);
        });
    }
}
