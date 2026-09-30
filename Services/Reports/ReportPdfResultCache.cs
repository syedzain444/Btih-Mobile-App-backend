using System.Collections.Concurrent;
using HospitalMobileAPPApi.Services.Reports.Models;

namespace HospitalMobileAPPApi.Services.Reports;

/// <summary>
/// Short-lived in-memory PDF cache so repeated Lab/Prescription opens
/// on the hospital network avoid regenerating QuestPDF every time (REQ-2026-020).
/// </summary>
public interface IReportPdfResultCache
{
    bool TryGet(string key, out byte[] pdfBytes);
    void Set(string key, byte[] pdfBytes);
    static string BuildKey(long rptId, string reportName, string parameters) =>
        $"{rptId}|{reportName.Trim().ToUpperInvariant()}|{parameters.Trim()}";
}

public sealed class ReportPdfResultCache : IReportPdfResultCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private const int MaxEntries = 64;

    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();

    public bool TryGet(string key, out byte[] pdfBytes)
    {
        pdfBytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(key)) return false;

        if (!_entries.TryGetValue(key, out var entry)) return false;
        if (DateTime.UtcNow - entry.CreatedUtc > Ttl)
        {
            _entries.TryRemove(key, out _);
            return false;
        }

        pdfBytes = entry.Bytes;
        return true;
    }

    public void Set(string key, byte[] pdfBytes)
    {
        if (string.IsNullOrWhiteSpace(key) || pdfBytes.Length == 0) return;

        _entries[key] = new CacheEntry(pdfBytes, DateTime.UtcNow);
        EvictIfNeeded();
    }

    private void EvictIfNeeded()
    {
        if (_entries.Count <= MaxEntries) return;

        foreach (var stale in _entries
                     .OrderBy(e => e.Value.CreatedUtc)
                     .Take(_entries.Count - MaxEntries)
                     .Select(e => e.Key)
                     .ToList())
        {
            _entries.TryRemove(stale, out _);
        }
    }

    private sealed record CacheEntry(byte[] Bytes, DateTime CreatedUtc);
}
