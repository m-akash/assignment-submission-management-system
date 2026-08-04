using AssignmentSystem.Domain.ClassCourses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AssignmentSystem.Infrastructure.Persistence.Configurations;

internal sealed class ClassCourseConfiguration : IEntityTypeConfiguration<ClassCourse>
{
    public void Configure(EntityTypeBuilder<ClassCourse> builder)
    {
        builder.ToTable("class_courses");

        builder.HasKey(cc => cc.Id);
        builder.Property(cc => cc.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(cc => cc.ClassId).IsRequired();
        builder.Property(cc => cc.CourseId).IsRequired();

        builder.Property(cc => cc.CreatedAtUtc).IsRequired();
        builder.Property(cc => cc.UpdatedAtUtc).IsRequired();
        builder.Property(cc => cc.CreatedBy);
        builder.Property(cc => cc.UpdatedBy);

        builder.Property(cc => cc.RowVersion).IsRowVersion();

        // Deleting a class takes its offerings with it — an offering has no meaning without
        // the class. A course, by contrast, is a catalogue entry shared across classes, so
        // it is protected while any class still studies it.
        builder.HasOne(cc => cc.Class)
            .WithMany(c => c.ClassCourses)
            .HasForeignKey(cc => cc.ClassId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cc => cc.Course)
            .WithMany()
            .HasForeignKey(cc => cc.CourseId)
            .OnDelete(DeleteBehavior.Restrict);

        // A class studies a course once. This is the constraint that makes the offering a
        // usable identity for everything downstream.
        builder.HasIndex(cc => new { cc.ClassId, cc.CourseId }).IsUnique();

        // Supports "which classes study this course?" without scanning.
        builder.HasIndex(cc => cc.CourseId);
    }
}
