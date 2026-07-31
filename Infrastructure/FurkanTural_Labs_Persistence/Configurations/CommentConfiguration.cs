using FurkanTural_Labs_Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurkanTural_Labs_Persistence.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Author).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Body).IsRequired().HasMaxLength(1000);

        builder.HasOne(x => x.Blog)
               .WithMany(x => x.Comments)
               .HasForeignKey(x => x.BlogId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.BlogId);
    }
}
