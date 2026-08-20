using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RomaERP.Domain.HR;

namespace RomaERP.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.Property(d => d.Code).HasMaxLength(20).IsRequired();
        builder.Property(d => d.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(d => d.NameEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(d => d.Code).IsUnique();

        builder.HasOne(d => d.ParentDepartment)
            .WithMany(d => d.Children)
            .HasForeignKey(d => d.ParentDepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.Property(p => p.Code).HasMaxLength(20).IsRequired();
        builder.Property(p => p.TitleAr).HasMaxLength(200).IsRequired();
        builder.Property(p => p.TitleEn).HasMaxLength(200).IsRequired();
        builder.HasIndex(p => p.Code).IsUnique();

        builder.HasOne(p => p.Department)
            .WithMany(d => d.Positions)
            .HasForeignKey(p => p.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.Property(e => e.EmployeeCode).HasMaxLength(20).IsRequired();
        builder.Property(e => e.FullNameAr).HasMaxLength(200).IsRequired();
        builder.Property(e => e.FullNameEn).HasMaxLength(200).IsRequired();
        builder.Property(e => e.NationalId).HasMaxLength(20);
        builder.Property(e => e.Email).HasMaxLength(150);
        builder.Property(e => e.Phone).HasMaxLength(30);
        builder.Property(e => e.BasicSalary).HasPrecision(18, 2);
        builder.HasIndex(e => e.EmployeeCode).IsUnique();

        builder.HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Position)
            .WithMany(p => p.Employees)
            .HasForeignKey(e => e.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}

public class SalaryComponentConfiguration : IEntityTypeConfiguration<SalaryComponent>
{
    public void Configure(EntityTypeBuilder<SalaryComponent> builder)
    {
        builder.Property(c => c.Code).HasMaxLength(20).IsRequired();
        builder.Property(c => c.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(c => c.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(c => c.DefaultValue).HasPrecision(18, 2);
        builder.HasIndex(c => c.Code).IsUnique();

        builder.HasOne(c => c.LinkedAccount)
            .WithMany()
            .HasForeignKey(c => c.LinkedAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}

public class EmployeeSalaryComponentConfiguration : IEntityTypeConfiguration<EmployeeSalaryComponent>
{
    public void Configure(EntityTypeBuilder<EmployeeSalaryComponent> builder)
    {
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.EmployeeId, x.SalaryComponentId }).IsUnique();

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.SalaryComponents)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SalaryComponent)
            .WithMany()
            .HasForeignKey(x => x.SalaryComponentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.Property(r => r.Description).HasMaxLength(500);

        builder.HasOne(r => r.FiscalPeriod)
            .WithMany()
            .HasForeignKey(r => r.FiscalPeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.JournalEntry)
            .WithMany()
            .HasForeignKey(r => r.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Lines)
            .WithOne(l => l.PayrollRun)
            .HasForeignKey(l => l.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}

public class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
    {
        builder.Property(l => l.BasicSalary).HasPrecision(18, 2);
        builder.Property(l => l.TotalAllowances).HasPrecision(18, 2);
        builder.Property(l => l.TotalDeductions).HasPrecision(18, 2);
        builder.Property(l => l.NetSalary).HasPrecision(18, 2);

        builder.HasOne(l => l.Employee)
            .WithMany()
            .HasForeignKey(l => l.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
