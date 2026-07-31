using FurkanTural_Labs_Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurkanTural_Labs_Persistence.Configurations;

public class BlogCategoryConfiguration : IEntityTypeConfiguration<BlogCategory>
{
    public void Configure(EntityTypeBuilder<BlogCategory> builder)
    {
        builder.ToTable("BlogCategories");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Blog)
               .WithMany(x => x.BlogCategories)
               .HasForeignKey(x => x.BlogId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Category)
               .WithMany(x => x.BlogCategories)
               .HasForeignKey(x => x.CategoryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BlogId, x.CategoryId }).IsUnique();
    }
}
