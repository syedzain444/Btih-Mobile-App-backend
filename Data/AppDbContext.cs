using HospitalMobileAPPApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace HospitalMobileAPPApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        //public DbSet<BILL_CAT> Employees { get; set; }
        public DbSet<PatientProfile> PatientProfile { get; set; }
        public DbSet<PatientInformation> PatientInformation { get; set; }
        public DbSet<PatientMst> PatientMst { get; set; }
        public DbSet<DoctorInfo> DoctorInfo { get; set; }
        public DbSet<DoctorSchedule> DoctorSchedule { get; set; }
        public DbSet<PrescriptionModel> PrescriptionModel { get; set; }
        public DbSet<BillingHistoryModel> BillingHistoryModel { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PatientInformation>()
                .ToTable("PATIENT_INFORMATION")
        .HasNoKey();

            modelBuilder.Entity<PatientMst>()
                .ToTable("PATIENT_MST")
        .HasNoKey();
        }
    }
}