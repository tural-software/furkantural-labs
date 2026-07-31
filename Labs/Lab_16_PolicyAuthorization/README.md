# Lab_16 — Policy-Based Authorization

İlgili yazı: [#16 — Policy-Based Authorization — Rol Adını Controller'a Yazmak Neden Yetki Kontrolü Sayılmaz?](https://blog.furkantural.com/Home/Post/16)

## İddia

Her senaryoda `furkan` adlı kullanıcının 7 numaralı yazısı düzenlenmeye çalışılıyor.
Ölçülen şey, isteği gönderenin aldığı **durum kodu**.

## Kanıt

```text
Senaryo                               Durum  Beklenen      Süre     Bellek  Sonuç
───────────────────────────────────  ──────  ─────────  ───────  ─────────  ─────
1) Rol attribute, başkasının yazısı     200  = 200        92 ms     245 KB  GEÇTİ
2) Policy, başkasının yazısı            403  = 403        22 ms     346 KB  GEÇTİ
3) Policy, yazının sahibi               200  = 200         4 ms      75 KB  GEÇTİ
4) Policy, admin                        200  = 200         6 ms      28 KB  GEÇTİ
5) Succeed + Fail aynı requirement      403  = 403         1 ms      11 KB  GEÇTİ
6) Handler hiçbir şey çağırmıyor        403  = 403         0 ms      11 KB  GEÇTİ
```

Okuma notları:

- **1 ile 2'yi yan yana koyun.** Aynı kullanıcı, aynı rol, aynı istek. Rol adına bakan
  uygulama 200 döndü: `ayse` Editor rolünde olduğu için başkasının yazısını düzenleyebildi.
  Kural kaynağa bakınca aynı istek 403 alıyor. `[Authorize(Roles = "...")]` "bu kişi kim"
  sorusunu cevaplar; iş kuralı çoğu zaman "bu kişi **bu kayda** ne yapabilir"dir.
- **3 ile 4, kuralın tamamının tek yerde olduğunu gösterir.** Sahiplik de admin istisnası
  da handler'ın içinde; uçta kopyalanan bir `if` yok. Kural değişince taranacak tek dosya var.
- **5, yazıdaki tablonun en kritik satırı.** Aynı requirement için kayıtlı handler'lar VEYA
  gibi davranır — biri `Succeed` derse yeter. Ama `Fail()` bunu ezer: burada bir handler
  izin verdi, diğeri yasakladı, sonuç ret. Kesin yasak koyacaksanız çağıracağınız şey budur.
- **6, sessiz açığın kaynağı.** Handler hiçbir şey çağırmadan döndü. Bu "izin verdim"
  değil "karar veremedim" demektir; requirement'ı kimse karşılamadığı için sonuç ret.
  Ters yönde okunursa — "reddetmedim, demek ki geçer" — yazılan handler hiçbir şey korumaz.
- **Handler'lar scoped kayıtlı.** Singleton kaydedip içine `DbContext` enjekte etmek
  captive dependency üretir; kaynağa bakan bir kuralın er geç veritabanına gitmesi gerekir.

## Çalıştır

```powershell
dotnet run
```

Veritabanı gerekmez; laboratuvar kendi sunucusunu rastgele bir portta ayağa kaldırır.

## Neden bu sayılar sabit

Kimlik `X-Lab-User` ve `X-Lab-Roles` başlıklarına bakan paylaşılan bir şema ile kuruluyor;
JWT kurmak yetki sorusuna hiçbir şey katmaz. Kaynağa bağlı karar attribute ile verilemediği
için uç, kaydı yükledikten sonra `IAuthorizationService` üzerinden kararı kendisi tetikler —
`if` hâlâ duruyor, ama içindeki **kural** artık controller'da değil.
