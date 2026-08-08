# OSINT Scan Report - Session #10

**Target:** `ahmadswailemals@hotmail.com` (Email)  
**Status:** `Completed`  
**Started At:** 2026-07-29 01:31:32 UTC  
**Completed At:** 2026-07-29 01:31:33 UTC  

---

## Findings Summary

### [Medium] Email Identity Analysis: ahmadswailemals@hotmail.com
* **Module:** `EmailInfo`
* **Timestamp:** 2026-07-29 01:31:33 UTC
* **Summary:** Validated email 'ahmadswailemals@hotmail.com'. Identified 2 past data breach hits for target email.

```json
{
  "Email": "ahmadswailemals@hotmail.com",
  "Username": "ahmadswailemals",
  "Domain": "hotmail.com",
  "IsFormatValid": true,
  "IsDisposable": false,
  "HasMxRecords": true,
  "BreachesCount": 2,
  "BreachDetails": [
    {
      "Title": "Collection #1 Breach",
      "Date": "2019-01-07",
      "CompromisedData": [
        "Email",
        "Password"
      ]
    },
    {
      "Title": "Canva Data Leak",
      "Date": "2019-05-24",
      "CompromisedData": [
        "Email",
        "Name",
        "City"
      ]
    }
  ]
}
```

