using Flit.Infrastructure.Persistence.Joins;
using Flit.Modules.Rbac.Domain;
using Flit.Modules.Users.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Flit.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configurations para el schema `rbac` (ADR-0011).
/// Validado contra docs/sql/flit-v2-initial-schema.sql.
/// </summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles", "rbac");

        b.HasKey(r => r.Id);
        b.Property(r => r.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(r => r.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        b.HasIndex(r => r.Code).IsUnique().HasDatabaseName("ix_roles_code");

        b.Property(r => r.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(r => r.Description).HasColumnName("description").HasMaxLength(500);
        b.Property(r => r.IsSystem).HasColumnName("is_system").HasDefaultValue(false).IsRequired();
        b.Property(r => r.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        b.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions", "rbac");

        b.HasKey(p => p.Id);
        b.Property(p => p.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(p => p.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.HasIndex(p => p.Code).IsUnique().HasDatabaseName("ix_permissions_code");

        b.Property(p => p.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(p => p.Description).HasColumnName("description").HasMaxLength(500);
        b.Property(p => p.Module).HasColumnName("module").HasMaxLength(50).IsRequired();
        b.HasIndex(p => p.Module).HasDatabaseName("ix_permissions_module");

        b.Property(p => p.IsSystem).HasColumnName("is_system").HasDefaultValue(false).IsRequired();
        b.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> b)
    {
        b.ToTable("menu_items", "rbac");

        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        b.Property(m => m.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.HasIndex(m => m.Code).IsUnique().HasDatabaseName("ix_menu_items_code");

        b.Property(m => m.ParentId).HasColumnName("parent_id");
        b.HasIndex(m => m.ParentId).HasDatabaseName("ix_menu_items_parent");

        // Self-FK
        b.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(m => m.ParentId)
            .HasConstraintName("fk_mi_parent")
            .OnDelete(DeleteBehavior.SetNull);

        b.Property(m => m.Label).HasColumnName("label").HasMaxLength(150).IsRequired();
        b.Property(m => m.Icon).HasColumnName("icon").HasMaxLength(50);
        b.Property(m => m.FrontendPath).HasColumnName("frontend_path").HasMaxLength(255);
        b.Property(m => m.SortOrder).HasColumnName("sort_order").HasDefaultValue((short)0).IsRequired();
        b.HasIndex(m => m.SortOrder).HasDatabaseName("ix_menu_items_sort");

        b.Property(m => m.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        b.Property(m => m.IsVisible).HasColumnName("is_visible").HasDefaultValue(true).IsRequired();
        b.Property(m => m.IsSeparator).HasColumnName("is_separator").HasDefaultValue(false).IsRequired();
        b.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}

internal sealed class UserRoleEntityConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    public void Configure(EntityTypeBuilder<UserRoleEntity> b)
    {
        b.ToTable("user_roles", "rbac");

        b.HasKey(ur => new { ur.UserId, ur.RoleId });
        b.Property(ur => ur.UserId).HasColumnName("user_id");
        b.Property(ur => ur.RoleId).HasColumnName("role_id");
        b.Property(ur => ur.AssignedAt).HasColumnName("assigned_at").IsRequired();
        b.Property(ur => ur.AssignedByUserId).HasColumnName("assigned_by_user_id");

        b.HasOne<User>()
            .WithMany()
            .HasForeignKey(ur => ur.UserId)
            .HasConstraintName("fk_ur_user")
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Role>()
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .HasConstraintName("fk_ur_role")
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(ur => ur.RoleId).HasDatabaseName("ix_user_roles_role_id");
    }
}

internal sealed class RolePermissionEntityConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<RolePermissionEntity> b)
    {
        b.ToTable("role_permissions", "rbac");

        b.HasKey(rp => new { rp.RoleId, rp.PermissionId });
        b.Property(rp => rp.RoleId).HasColumnName("role_id");
        b.Property(rp => rp.PermissionId).HasColumnName("permission_id");
        b.Property(rp => rp.AssignedAt).HasColumnName("assigned_at").IsRequired();
        b.Property(rp => rp.AssignedByUserId).HasColumnName("assigned_by_user_id");

        b.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rp => rp.RoleId)
            .HasConstraintName("fk_rp_role")
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Permission>()
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId)
            .HasConstraintName("fk_rp_permission")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class RoleMenuItemEntityConfiguration : IEntityTypeConfiguration<RoleMenuItemEntity>
{
    public void Configure(EntityTypeBuilder<RoleMenuItemEntity> b)
    {
        b.ToTable("role_menu_items", "rbac");

        b.HasKey(rmi => new { rmi.RoleId, rmi.MenuItemId });
        b.Property(rmi => rmi.RoleId).HasColumnName("role_id");
        b.Property(rmi => rmi.MenuItemId).HasColumnName("menu_item_id");

        b.HasOne<Role>()
            .WithMany()
            .HasForeignKey(rmi => rmi.RoleId)
            .HasConstraintName("fk_rmi_role")
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(rmi => rmi.MenuItemId)
            .HasConstraintName("fk_rmi_mi")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
