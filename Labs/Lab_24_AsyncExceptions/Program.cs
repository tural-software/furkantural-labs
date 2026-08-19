using FurkanTural_Labs_Application.Diagnostics;
using Lab_24_AsyncExceptions;

// Her senaryoda aynı üç iş çalışır ve üçü de LabException fırlatır. Değişen tek şey
// çağıranın onları nasıl beklediği. Ölçülen sütun, çağıranın `catch (LabException)`
// bloğuna ulaşan istisna sayısı — yani "haberim oldu mu" sorusunun sayısal karşılığı.
//
// Beklentiler ölçümden değil dilin kurallarından geliyor:
//   · await, birden çok hatadan yalnız ilkini yeniden fırlatır → 1
//   · Task.Exception bütün hataları AggregateException içinde tutar → 3
//   · sıralı await'te ilk hata kalan işleri hiç başlatmaz → 1 (ve 2 iş çalışmaz)
//   · beklenmeyen task ile async void, çağırana hiçbir şey döndürmez → 0
//   · bloke eden .Result istisnayı AggregateException'a sarar → beklenen tip tutmaz → 0
//   · GetAwaiter().GetResult() sarmalamayı açar → 1

var report = new LabReport(
    title: "Lab_24 — Üç iş hata fırlattı, kaçından haberiniz oldu",
    claim: $"Her senaryoda aynı {Work.FailingJobs} iş çalışıyor ve {Work.FailingJobs}'ü de hata fırlatıyor. " +
           "Ölçülen şey, çağıranın catch bloğuna ulaşan istisna sayısı.",
    metric: "Görülen");

// ── 1. Paralel bekleme: hatanın çoğunluğu sessizce düşer ─────────────────────
// En sık yazılan biçim. Üç iş de çalışır, üçü de patlar, tek satır log düşer.
await report.MeasureAsync(
    "1) await Task.WhenAll",
    Expectation.Exactly(1),
    async () =>
    {
        var caught = 0;
        try
        {
            await Task.WhenAll(Work.StartAll());
        }
        catch (LabException)
        {
            caught++;
        }

        return caught;
    },
    note: $"{Work.FailingJobs} iş de çalıştı ve {Work.FailingJobs}'ü de patladı; await yalnız ilkini yeniden fırlattı. " +
          "Diğer iki hata kayıp değil, sorulmamış durumda.");

// ── 2. Aynı çağrı, hataların tamamı sorulunca ────────────────────────────────
// Task nesnesi elde tutulursa bütün hatalar AggregateException içinde duruyor.
await report.MeasureAsync(
    "2) WhenAll + Exception.InnerExceptions",
    Expectation.Exactly(Work.FailingJobs),
    async () =>
    {
        var caught = 0;
        var all = Task.WhenAll(Work.StartAll());
        try
        {
            await all;
        }
        catch (LabException)
        {
            caught += all.Exception!.InnerExceptions.Count;
        }

        return caught;
    },
    note: "1. senaryonun aynısı, tek fark task'ın elde tutulması. Hatalar hep oradaydı; " +
          "eksik olan await değil, sorgulama.");

// ── 3. Sıralı bekleme: ilk hata kalanları hiç başlatmaz ──────────────────────
// Burada 1 görülmesinin sebebi 1. senaryodakiyle aynı değil: orada üç iş de çalışmıştı,
// burada ikisi hiç denenmedi. Aynı sayı, bambaşka bir durum.
await report.MeasureAsync(
    "3) Sıralı await, tek try",
    Expectation.Exactly(1),
    async () =>
    {
        var caught = 0;
        try
        {
            for (var i = 1; i <= Work.FailingJobs; i++)
                _ = await Work.FailAsync(i);
        }
        catch (LabException)
        {
            caught++;
        }

        return caught;
    },
    note: "İlk iş patlayınca döngü kırıldı; 2 iş hiç çalışmadı. Toplu işlerde " +
          "\"bir kayıt bozuktu, kalanı da işlenmedi\" tablosu tam olarak budur.");

// ── 4. Aynı sıra, iş başına try ──────────────────────────────────────────────
// Hem üç iş de çalışıyor hem üç hata da görülüyor. Sıralı işlemenin doğru biçimi.
await report.MeasureAsync(
    "4) Sıralı await, iş başına try",
    Expectation.Exactly(Work.FailingJobs),
    async () =>
    {
        var caught = 0;
        for (var i = 1; i <= Work.FailingJobs; i++)
        {
            try
            {
                _ = await Work.FailAsync(i);
            }
            catch (LabException)
            {
                caught++;
            }
        }

        return caught;
    },
    note: "try döngünün içine alındı: üç iş de çalıştı, üç hata da görüldü. " +
          "3. senaryodan farkı tek bir parantezin yeri.");

// ── 5. Beklenmeyen task: hata hiçbir yere gitmiyor ───────────────────────────
// "Zaten arka planda çalışsın" diye await'in atıldığı yer. Çağıranın catch'i
// task'a bağlı olmadığı için hiçbir şey görmez; süreç de uyarı vermez.
await report.MeasureAsync(
    "5) Beklenmeyen task",
    Expectation.Exactly(0),
    async () =>
    {
        var caught = 0;
        Task<int>[] pending = [];
        try
        {
            pending = Work.StartAll();
        }
        catch (LabException)
        {
            caught++;
        }

        // Ölçüm yukarıda bitti. Bu satır yalnız temizlik: gözlenmemiş hatalar
        // sonraki senaryolara taşınmasın.
        await Work.ObserveAsync(pending);
        return caught;
    },
    note: "Üç iş de patladı, çağıran hiçbirini görmedi. Kaybı fark ettiren tek şey " +
          "işin yapılmamış olması — o da günler sonra.");

// ── 6. async void: istisna çağırana değil bağlama gider ──────────────────────
// try bloğunun içinden çağrılıyor olması hiçbir şey değiştirmiyor: async void
// istisnayı SynchronizationContext'e gönderir. Konsolda bağlam olmadığı için
// istisna doğrudan süreci öldürürdü; ölçüm yapılabilsin diye en küçük bağlam kuruluyor.
await report.MeasureAsync(
    "6) async void",
    Expectation.Exactly(0),
    async () =>
    {
        var caught = 0;
        var previous = SynchronizationContext.Current;
        var context = new CapturingSynchronizationContext(Work.FailingJobs);
        SynchronizationContext.SetSynchronizationContext(context);

        try
        {
            for (var i = 1; i <= Work.FailingJobs; i++)
                Work.FailVoid(i);
        }
        catch (LabException)
        {
            caught++;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        await context.DrainAsync();
        return caught;
    },
    note: $"try bloğu boş döndü; {Work.FailingJobs} istisnanın {Work.FailingJobs}'ü de bağlama düştü. " +
          "Bağlamı olmayan bir konsolda aynı kod süreci sonlandırırdı.");

// ── 7. Bloke etmek: istisna sarmalanır, catch tipi tutmaz ────────────────────
// .Result / .Wait() istisnayı AggregateException içine koyar. Kod aynı kalır,
// catch (LabException) sessizce devre dışı kalır.
await report.MeasureAsync(
    "7) .Result ile bloke",
    Expectation.Exactly(0),
    () =>
    {
        var caught = 0;
        var all = Task.WhenAll(Work.StartAll());
        try
        {
            _ = all.Result;
        }
        catch (LabException)
        {
            caught++;
        }
        catch (AggregateException)
        {
            // Gerçekte fırlatılan bu. Sayılmıyor: ölçülen şey çağıranın beklediği
            // tipin yakalanıp yakalanmadığı.
        }

        return Task.FromResult(caught);
    },
    note: "Hata görüldü ama beklenen tipte değil: AggregateException'a sarılmış. " +
          "Async'e geçerken çalışan catch blokları bu yüzden sessizce devre dışı kalır.");

// ── 8. Aynı bloke etme, sarmalamayı açan çağrı ───────────────────────────────
// Bloke etmek zorunda kalınan yerde en azından istisna tipi korunur.
await report.MeasureAsync(
    "8) GetAwaiter().GetResult()",
    Expectation.Exactly(1),
    () =>
    {
        var caught = 0;
        try
        {
            _ = Task.WhenAll(Work.StartAll()).GetAwaiter().GetResult();
        }
        catch (LabException)
        {
            caught++;
        }

        return Task.FromResult(caught);
    },
    note: "7. senaryonun aynısı, tek fark çağrı biçimi: sarmalama açıldığı için " +
          "catch tuttu. Bloke etmenin diğer bedelleri duruyor, kaybolan istisna gitti.");

return report.Print();
