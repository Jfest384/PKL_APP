using PKL_API.Controllers;
using PKL_API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PKL_API.Helpers
{
    public static class PrintHelper
    {
        public static void BuildReportTable(
            IContainer container,
            List<(Student student, List<Report> reports)> studentRows
        )
        {
            container.Table(table =>
            {
                // Struktur kolom
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);   // No
                    columns.ConstantColumn(62);   // NIS
                    columns.RelativeColumn(1.5f); // Nama
                    columns.ConstantColumn(73);   // Tanggal
                    columns.RelativeColumn(2.5f); // Deskripsi
                    columns.ConstantColumn(75);   // Mentor
                    columns.ConstantColumn(75);   // Walas
                    columns.ConstantColumn(75);   // Kajur
                });

                // Header tabel
                table.Cell().RowSpan(2u).Element(CellStyle).Text("No").Bold().AlignCenter();
                table.Cell().RowSpan(2u).Element(CellStyle).Text("NIS").Bold().AlignCenter();
                table.Cell().RowSpan(2u).Element(CellStyle).Text("Nama").Bold().AlignCenter();
                table.Cell().RowSpan(2u).Element(CellStyle).Text("Tanggal").Bold().AlignCenter();
                table.Cell().RowSpan(2u).Element(CellStyle).Text("Deskripsi").Bold().AlignCenter();
                table.Cell().ColumnSpan(3u).Element(CellStyle).Text("Feedback").Bold().AlignCenter();
                table.Cell().Element(CellStyle).Text("Mentor").Bold().AlignCenter();
                table.Cell().Element(CellStyle).Text("Walas").Bold().AlignCenter();
                table.Cell().Element(CellStyle).Text("Kajur").Bold().AlignCenter();

                // Data rows
                int no = 1;
                foreach (var (student, reports) in studentRows)
                {
                    int reportCount = Math.Max(1, reports.Count);
                    for (int i = 0; i < reportCount; i++)
                    {
                        if (i == 0)
                        {
                            // Merge baris untuk kolom tetap
                            table.Cell().RowSpan((uint)reportCount).Element(CellStyle).AlignCenter().Text(no.ToString());
                            table.Cell().RowSpan((uint)reportCount).Element(CellStyle).Text(student.nis ?? "").AlignCenter();
                            table.Cell().RowSpan((uint)reportCount).Element(CellStyle).Text(student.User?.fullname ?? "");
                        }

                        var report = reports.ElementAtOrDefault(i);
                        table.Cell().Element(CellStyle).AlignCenter().Text(report != null ? report.date.ToString("dd/MM/yyyy") : "-");
                        table.Cell().Element(CellStyle).Text(report?.description ?? "-");
                        table.Cell().Element(CellStyle).Text(report?.ReportFeedback?.mentor ?? "-");
                        table.Cell().Element(CellStyle).Text(report?.ReportFeedback?.walas ?? "-");
                        table.Cell().Element(CellStyle).Text(report?.ReportFeedback?.kajur ?? "-");
                    }
                    no++;
                }
            });

            // Style untuk setiap cell
            IContainer CellStyle(IContainer container) =>
                container
                    .BorderColor(Colors.Grey.Lighten2)
                    .Border(1)
                    .PaddingVertical(4)
                    .PaddingHorizontal(4)
                    .AlignMiddle();
        }

        public static byte[] GenerateStudentReportPdf(
            Student student,
            Report? report,
            DateOnly date
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(15).Text($"Presensi PKL - {student.nis}")
                            .FontSize(16).Bold().AlignCenter().LineHeight(2);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(100);
                                c.ConstantColumn(75);
                                c.RelativeColumn();
                            });

                            table.Cell().ColumnSpan(2).Element(CellStyle).Text("Nama Siswa").Bold();
                            table.Cell().Element(CellStyle).Text(student.User?.fullname ?? "-");

                            table.Cell().ColumnSpan(2).Element(CellStyle).Text("Kelas").Bold();
                            table.Cell().Element(CellStyle).Text(student.Classroom?.name ?? "-");

                            table.Cell().ColumnSpan(2).Element(CellStyle).Text("Nama Mentor").Bold();
                            table.Cell().Element(CellStyle).Text(student.Mentor?.User?.fullname ?? "-");

                            table.Cell().ColumnSpan(2).Element(CellStyle).Text("Tempat PKL").Bold();
                            table.Cell().Element(CellStyle).Text(student.Company?.name ?? "-");

                            table.Cell().ColumnSpan(2).Element(CellStyle).Text("Tanggal").Bold();
                            table.Cell().Element(CellStyle).Text(ReportController.ToIndonesianLongDate(date));

                            table.Cell().ColumnSpan(2).Element(CellStyle).Text("Deskripsi").Bold();
                            table.Cell().Element(CellStyle).Text(report?.description ?? "-");

                            table.Cell().RowSpan(3).Element(CellStyle).Text("Feedback").Bold();
                            table.Cell().Element(CellStyle).Text("Mentor").Bold();
                            table.Cell().Element(CellStyle).Text(report?.ReportFeedback?.mentor ?? "-");
                            table.Cell().Element(CellStyle).Text("Wali Kelas").Bold();
                            table.Cell().Element(CellStyle).Text(report?.ReportFeedback?.walas ?? "-");
                            table.Cell().Element(CellStyle).Text("Kepala Jurusan").Bold();
                            table.Cell().Element(CellStyle).Text(report?.ReportFeedback?.kajur ?? "-");
                        });

                        col.Item().PaddingTop(20).PaddingBottom(10).Text("Foto Bimbingan").Bold().AlignCenter();
                        col.Item().Element(border =>
                            border
                                .Border(1)
                                .BorderColor(Colors.Grey.Medium)
                                .Height(300)
                                .AlignCenter()
                                .AlignMiddle()
                                .Background(Colors.White)
                                .Element(inner =>
                                {
                                    if (report?.ReportPhoto != null)
                                        inner.AlignCenter().AlignMiddle().MaxHeight(260).Image(report.ReportPhoto.files, ImageScaling.FitArea);
                                    else
                                        inner.AlignCenter().AlignMiddle().Text("Tidak ada gambar").Italic();
                                })
                        );
                    });

                    IContainer CellStyle(IContainer container) =>
                        container.Border(1).BorderColor(Colors.Grey.Medium).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateClassReportPdf(
            Classroom classroom,
            List<(DateOnly weekStart, DateOnly weekEnd, List<(Student student, List<Report> reports)>)> weeklyData
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        foreach (var (weekStart, weekEnd, studentRows) in weeklyData)
                        {
                            col.Item().Column(headerCol =>
                            {
                                headerCol.Item().Text($"Bimbingan Laporan - {classroom.name}")
                                    .FontSize(16).Bold().AlignCenter().LineHeight(2);
                                headerCol.Item().Text($"{weekStart:dd MMMM yyyy}  -  {weekEnd:dd MMMM yyyy}")
                                    .FontSize(13).AlignCenter();
                            });

                            col.Item().PaddingVertical(10);
                            col.Item().Element(c => PrintHelper.BuildReportTable(c, studentRows));

                            if (studentRows.Count == 0)
                            {
                                col.Item().AlignCenter().AlignMiddle().Text(
                                    $"Tidak ada laporan bimbingan untuk minggu ini di kelas {classroom.name}."
                                ).FontSize(20).Bold().FontColor(Colors.Red.Darken2).LineHeight(18);
                            }

                            bool isLastWeek = (weekStart == weeklyData.Last().weekStart);
                            if (!isLastWeek)
                                col.Item().PageBreak();
                        }
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateMentorReportPdf(
            Mentor mentor,
            List<(DateOnly weekStart, DateOnly weekEnd, List<(Student student, List<Report> reports)>)> weeklyData
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        foreach (var (weekStart, weekEnd, studentRows) in weeklyData)
                        {
                            col.Item().Column(headerCol =>
                            {
                                headerCol.Item().Text($"Bimbingan Laporan - {mentor.User?.fullname ?? "-"}")
                                    .FontSize(16).Bold().AlignCenter().LineHeight(2);
                                headerCol.Item().Text($"{weekStart:dd MMMM yyyy}  -  {weekEnd:dd MMMM yyyy}")
                                    .FontSize(13).AlignCenter();
                            });

                            col.Item().PaddingVertical(10);
                            col.Item().Element(c => PrintHelper.BuildReportTable(c, studentRows));

                            bool isLastWeek = (weekStart == weeklyData.Last().weekStart);
                            if (!isLastWeek)
                                col.Item().PageBreak();
                        }
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateMentorWaliKelasReportPdf(
            string mentorName,
            List<(DateOnly weekStart, DateOnly weekEnd, List<(Student student, List<Report> reports)>)> weeklyData
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(11));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        foreach (var (weekStart, weekEnd, studentRows) in weeklyData)
                        {
                            col.Item().Column(headerCol =>
                            {
                                headerCol.Item().Text("Rekap Bimbingan Laporan")
                                    .FontSize(16).Bold().AlignCenter().LineHeight(2);
                                headerCol.Item().Text($"{weekStart:dd MMMM yyyy}  -  {weekEnd:dd MMMM yyyy}")
                                    .FontSize(13).AlignCenter();
                            });

                            col.Item().PaddingVertical(10);
                            col.Item().Element(c => PrintHelper.BuildReportTable(c, studentRows));

                            bool isLastWeek = (weekStart == weeklyData.Last().weekStart);
                            if (!isLastWeek)
                                col.Item().PageBreak();
                        }
                    });
                });
            }).GeneratePdf();
        }



        public static void BuildPresenceTable(
            IContainer container,
            List<Student> students,
            Dictionary<(int studentId, DateOnly date), Presence> presenceDict,
            DateOnly date
        )
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);   // No
                    columns.ConstantColumn(65);   // NIS
                    columns.RelativeColumn(2);    // Nama
                    columns.ConstantColumn(55);   // Status
                    columns.RelativeColumn(2);    // Report
                    columns.ConstantColumn(75);   // Mentor
                    columns.ConstantColumn(75);   // Walas
                    columns.ConstantColumn(75);   // Kajur
                });

                // Header tabel manual
                table.Cell().RowSpan(2).Element(CellStyle).Text("No").Bold().AlignCenter();
                table.Cell().RowSpan(2).Element(CellStyle).Text("NIS").Bold().AlignCenter();
                table.Cell().RowSpan(2).Element(CellStyle).Text("Nama").Bold().AlignCenter();
                table.Cell().RowSpan(2).Element(CellStyle).Text("Status").Bold().AlignCenter();
                table.Cell().RowSpan(2).Element(CellStyle).Text("Report").Bold().AlignCenter();
                table.Cell().ColumnSpan(3).Element(CellStyle).Text("Feedback").Bold().AlignCenter();
                table.Cell().Element(CellStyle).Text("Mentor").Bold().AlignCenter();
                table.Cell().Element(CellStyle).Text("Walas").Bold().AlignCenter();
                table.Cell().Element(CellStyle).Text("Kajur").Bold().AlignCenter();

                int rowNum = 1;
                foreach (var student in students)
                {
                    Presence? p = presenceDict.TryGetValue((student.id, date), out var pres) ? pres : null;
                    string status = "-";
                    string report = "-";
                    string mentorFeedback = "-";
                    string walasFeedback = "-";
                    string kajurFeedback = "-";

                    if (p != null)
                    {
                        status = p.PresenceType?.name ?? "-";
                        if (p.PresenceTypeid == 1 || p.PresenceTypeid == 5)
                            report = p.Detail?.daily_report ?? "-";
                        if (p.PresenceFeedback != null)
                        {
                            mentorFeedback = !string.IsNullOrWhiteSpace(p.PresenceFeedback.mentor) ? p.PresenceFeedback.mentor : "-";
                            walasFeedback = !string.IsNullOrWhiteSpace(p.PresenceFeedback.walas) ? p.PresenceFeedback.walas : "-";
                            kajurFeedback = !string.IsNullOrWhiteSpace(p.PresenceFeedback.kajur) ? p.PresenceFeedback.kajur : "-";
                        }
                    }

                    table.Cell().Element(CellStyle).Text(rowNum.ToString()).AlignCenter();
                    table.Cell().Element(CellStyle).Text(student.nis).AlignCenter();
                    table.Cell().Element(CellStyle).Text(student.User?.fullname ?? "-");
                    table.Cell().Element(CellStyle).Text(status).AlignCenter();
                    table.Cell().Element(CellStyle).Text(report);
                    table.Cell().Element(CellStyle).Text(mentorFeedback);
                    table.Cell().Element(CellStyle).Text(walasFeedback);
                    table.Cell().Element(CellStyle).Text(kajurFeedback);

                    rowNum++;
                }
            });

            IContainer CellStyle(IContainer container) =>
                container.Border(1)
                         .BorderColor(Colors.Grey.Lighten1)
                         .PaddingVertical(5)
                         .PaddingHorizontal(5)
                         .AlignMiddle();
        }

        public static byte[] GenerateClassPresenceMatrixPdf(
            Classroom classroom,
            List<Student> students,
            List<List<DateOnly>> dateChunks,
            Dictionary<(int studentId, DateOnly date), Presence> presenceDict
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        foreach (var chunk in dateChunks)
                        {
                            for (int i = 0; i < chunk.Count; i++)
                            {
                                var date = chunk[i];

                                col.Item().Column(headerCol =>
                                {
                                    headerCol.Item().Text($"Presensi PKL - {classroom.name}")
                                        .FontSize(16).Bold().AlignCenter().LineHeight(2);
                                    headerCol.Item().Text($"{PrecenseController.ToIndonesianLongDate(date)}")
                                        .FontSize(13).AlignCenter();
                                });

                                col.Item().PaddingVertical(10);
                                col.Item().Element(c => PrintHelper.BuildPresenceTable(c, students, presenceDict, date));
                                if (students.Count == 0)
                                {
                                    col.Item().AlignCenter().AlignMiddle().Text(
                                        $"Tidak ada siswa yang sedang PKL di kelas {classroom.name}."
                                    ).FontSize(22).Bold().FontColor(Colors.Red.Darken2).LineHeight(18);
                                }

                                bool isLastDateInChunk = (i == chunk.Count - 1);
                                bool isLastChunk = (chunk == dateChunks.Last());
                                if (!(isLastDateInChunk && isLastChunk))
                                    col.Item().PageBreak();
                            }
                        }
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateMentorPresenceMatrixPdf(
            Mentor mentor,
            List<Student> students,
            List<List<DateOnly>> dateChunks,
            Dictionary<(int studentId, DateOnly date), Presence> presenceDict
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        foreach (var chunk in dateChunks)
                        {
                            for (int i = 0; i < chunk.Count; i++)
                            {
                                var date = chunk[i];

                                col.Item().Column(headerCol =>
                                {
                                    headerCol.Item().Text($"Presensi PKL - {mentor.User?.fullname ?? "-"}")
                                        .FontSize(16).Bold().AlignCenter().LineHeight(2);
                                    headerCol.Item().Text($"{PrecenseController.ToIndonesianLongDate(date)}")
                                        .FontSize(13).AlignCenter();
                                });

                                col.Item().PaddingVertical(10);
                                col.Item().Element(c => PrintHelper.BuildPresenceTable(c, students, presenceDict, date));

                                bool isLastDateInChunk = (i == chunk.Count - 1);
                                bool isLastChunk = (chunk == dateChunks.Last());
                                if (!(isLastDateInChunk && isLastChunk))
                                    col.Item().PageBreak();
                            }
                        }
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] GenerateMentorWaliKelasPresencePdf(
            List<Student> students,
            List<List<DateOnly>> dateChunks,
            Dictionary<(int studentId, DateOnly date), Presence> presenceDict
        )
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(12));
                    page.PageColor(Colors.White);

                    page.Content().Column(col =>
                    {
                        foreach (var chunk in dateChunks)
                        {
                            for (int i = 0; i < chunk.Count; i++)
                            {
                                var date = chunk[i];

                                col.Item().Column(headerCol =>
                                {
                                    headerCol.Item().Text($"Rekap Presensi PKL")
                                        .FontSize(16).Bold().AlignCenter().LineHeight(2);
                                    headerCol.Item().Text($"{PrecenseController.ToIndonesianLongDate(date)}")
                                        .FontSize(13).AlignCenter();
                                });

                                col.Item().PaddingVertical(10);
                                col.Item().Element(c => PrintHelper.BuildPresenceTable(c, students, presenceDict, date));

                                bool isLastDateInChunk = (i == chunk.Count - 1);
                                bool isLastChunk = (chunk == dateChunks.Last());
                                if (!(isLastDateInChunk && isLastChunk))
                                    col.Item().PageBreak();
                            }
                        }
                    });
                });
            }).GeneratePdf();
        }
    }
}
