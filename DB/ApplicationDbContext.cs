using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using hackwknd_api.Models.DB;

namespace hackwknd_api.DB;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Chathistory> Chathistories { get; set; }

    public virtual DbSet<Note> Notes { get; set; }

    public virtual DbSet<Notesperusertag> Notesperusertags { get; set; }

    public virtual DbSet<Session> Sessions { get; set; }

    public virtual DbSet<User> Users { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Chathistory>(entity =>
        {
            entity.HasKey(e => e.Recid).HasName("chathistory_pkey");

            entity.ToTable("chathistory");

            entity.Property(e => e.Recid)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("recid");
            entity.Property(e => e.Chathistory1)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("chathistory");
            entity.Property(e => e.Createdateutc).HasColumnName("createdateutc");
            entity.Property(e => e.Lastupdateutc).HasColumnName("lastupdateutc");
            entity.Property(e => e.Userrecid).HasColumnName("userrecid");
        });

        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasKey(e => e.Recid).HasName("syllable_pkey");

            entity.ToTable("notes");

            entity.Property(e => e.Recid)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("recid");
            entity.Property(e => e.Createdateutc).HasColumnName("createdateutc");
            entity.Property(e => e.Datacontent).HasColumnName("datacontent");
            entity.Property(e => e.Extdata)
                .HasDefaultValueSql("'{}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("extdata");
            entity.Property(e => e.Lastupdateutc).HasColumnName("lastupdateutc");
            entity.Property(e => e.Userrecid).HasColumnName("userrecid");
        });

        modelBuilder.Entity<Notesperusertag>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("notesperusertag");

            entity.Property(e => e.Recid).HasColumnName("recid");
            entity.Property(e => e.Tag).HasColumnName("tag");
            entity.Property(e => e.Userrecid).HasColumnName("userrecid");
        });

        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Recid).HasName("session_pkey");

            entity.ToTable("session");

            entity.Property(e => e.Recid)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("recid");
            entity.Property(e => e.Createdateutc).HasColumnName("createdateutc");
            entity.Property(e => e.Generatedsessionkey).HasColumnName("generatedsessionkey");
            entity.Property(e => e.Lastupdateutc).HasColumnName("lastupdateutc");
            entity.Property(e => e.Userrecid).HasColumnName("userrecid");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Recid).HasName("user_pkey");

            entity.ToTable("user");

            entity.Property(e => e.Recid)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("recid");
            entity.Property(e => e.Createdateutc).HasColumnName("createdateutc");
            entity.Property(e => e.Lastupdateutc).HasColumnName("lastupdateutc");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
