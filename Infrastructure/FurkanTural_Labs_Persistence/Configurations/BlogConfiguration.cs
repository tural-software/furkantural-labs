using FurkanTural_Labs_Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FurkanTural_Labs_Persistence.Configurations;

public class BlogConfiguration : IEntityTypeConfiguration<Blog>
{
    public void Configure(EntityTypeBuilder<Blog> builder)
    {
        builder.ToTable("Blogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(250);
        builder.Property(x => x.Content).IsRequired();

        // Lab_12: aynı anı iki farklı tipte saklıyoruz. datetime2 zaman dilimini
        // taşımaz; datetimeoffset taşır. Fark laboratuvarda okunarak gösterilecek.
        builder.Property(x => x.PublishedAt).HasColumnType("datetime2");
        builder.Property(x => x.PublishedAtOffset).HasColumnType("datetimeoffset");

        // Lab_17 (Skip/Take) sıralama için indeksli bir sütun ister.
        builder.HasIndex(x => x.PublishedAt);
    }
}
