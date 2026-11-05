# REV-05 — Autorizacija revizija i audit integracija

## 1. Cilj

REV-05 obezbjedjuje eksplicitnu autorizaciju osjetljivih akcija
nad revizijama laboratorija i korisnickim nalozima.

Obuhvacene akcije:

- revision.submit
- revision.approve
- revision.return
- account.unlock

Sistem koristi podrazumijevano odbijanje (default deny).
Nepoznata akcija ili akcija bez registrovane politike ne smije
automatski dobiti pristup.

## 2. Arhitektura

Autorizacija koristi postojeci Platform.Access modul:

- IActionCatalog — poznate akcije
- IAccessPolicyRegistry — registrovane politike
- IAccessDecision — konacna odluka o pristupu

REV-05 implementira:

- RevisionActions
- RevisionApprovalPolicy
- RevisionActionPolicy
- RevisionAuthorReader
- RevisionAuthorizationRegistration
- DenyRevisionPermissions

Postojeci Platform.Access modul nije mijenjan.

## 3. Bezbjednosna matrica

| Subjekt | Akcija | Resurs | Odluka / uslov |
|---|---|---|---|
| Neautentifikovan korisnik | Sve cetiri akcije | Odgovarajuci resurs | Deny |
| Blokiran ili arhiviran korisnik | Sve cetiri akcije | Odgovarajuci resurs | Deny |
| Korisnik bez eksplicitne dozvole | Sve cetiri akcije | Odgovarajuci resurs | Deny |
| Ovlasteni korisnik | revision.submit | Revision | Allow ako pouzdani izvor potvrdi dozvolu |
| Ovlasteni korisnik | revision.return | Revision | Allow ako pouzdani izvor potvrdi dozvolu |
| Ovlasteni korisnik koji nije autor | revision.approve | Revision | Allow ako postoji eksplicitna dozvola |
| Autor revizije | revision.approve | Sopstvena revizija | Deny |
| Ovlasteni korisnik | account.unlock | Account | Allow ako pouzdani izvor potvrdi dozvolu |
| Bilo koji korisnik | Nepoznata akcija | Bilo koji resurs | Deny |

Allow u matrici predstavlja autorizacionu odluku, a ne
garanciju uspjesnog izvrsavanja poslovne operacije.

## 4. Zabrana samoodobravanja

RevisionApprovalPolicy:

1. Provjerava identitet i aktivno stanje korisnika.
2. Provjerava akciju i identifikator revizije.
3. Ucitava AuthorId iz serverskog skladista.
4. Odbija zahtjev ako je korisnik autor revizije.
5. Provjerava eksplicitno pravo odobravanja.

AuthorId se ne preuzima iz korisnickog HTTP zahtjeva.

Matijin domenski model sadrzi dodatnu provjeru
CoursePublishPolicy, koja zavisi od
RequiresIndependentApproval. REV-05 politika samoodobravanje
odbija bez obzira na tu postavku.

## 5. Tok podataka i granice povjerenja

Klijent
  |
  v
API / autentifikovani identitet
  |
  v
IAccessDecision
  |
  +--> Katalog akcija
  |
  +--> Registrovana REV-05 politika
           |
           +--> Pouzdani podaci o reviziji
           |
           +--> Pouzdani izvor dozvola
  |
  v
Allow / Deny
  |
  +--> Audit odluke o pristupu

API mora pozvati autorizaciju prije izvrsavanja
zasticene poslovne operacije.

Identitet korisnika, autor revizije i dozvole predstavljaju
bezbjednosno znacajne podatke i moraju dolaziti iz
pouzdanih serverskih izvora.

## 6. Audit

REV-05 ima pripremljene komponente:

- RevisionAuditEventFactory
- RevisionAccessAuditWriter
- IRevisionAuditContextProvider

Trenutno je podrzano formiranje dogadjaja access.denied
za odbijene REV-05 zahtjeve.

Adapter koristi ITrustedAuditWriter i provjerava rezultat
upisa. Rejected i Unavailable ne smatra uspjehom.

Adapter jos nije aktiviran u produkcionoj DI registraciji,
jer AddAudit trenutno ne registruje ITrustedAuditWriter,
a pouzdan AuditContext provider jos nije implementiran.

Audit uspjesno izvrsenih operacija zahtijeva uskladjivanje
sa katalogom audit dogadjaja i vlasnicima poslovnih tokova.

## 7. Trenutna ogranicenja

DenyRevisionPermissions je privremena fail-closed
implementacija. Dok se ne poveze pouzdan izvor dozvola,
sve cetiri REV-05 akcije se odbijaju.

Politike su registrovane u Program.cs, ali stvarni
poslovni endpointi moraju koristiti IAccessDecision
prije izvrsavanja operacija.

Potrebna je koordinacija sa vlasnicima Identity, Access,
Audit i API modula.

## 8. Testiranje

Testovi se nalaze u:

backend/Platform.Tests/RevisionAuthorization

Pokretanje:

dotnet test backend/Platform.Tests/Platform.Tests.csproj --filter "FullyQualifiedName~RevisionAuthorization"

Posljednji provjereni rezultat:

27 testova uspjesno, 0 neuspjesnih.

Testovi pokrivaju osnovne pozitivne i negativne
slucajeve autorizacije, generisanje audit dogadjaja
i ponasanje audit adaptera.

Testovi koriste fake izvore dozvola i audit writer.
Ne dokazuju funkcionalnost stvarnih API endpointa
niti trajno cuvanje audit dogadjaja.

## 9. Granice odgovornosti

REV-05:
- Autorizacione politike
- Eksplicitne akcije i default deny
- Zabrana samoodobravanja
- Audit adapter
- Unit testovi i bezbjednosna dokumentacija

Poslovni API tokovi, domenski model i persistence
ostaju odgovornost njihovih vlasnika.

## 10. Preostala integracija

- Povezati stvarni izvor eksplicitnih dozvola.
- Obezbijediti pouzdan audit kontekst.
- Aktivirati audit adapter nakon registracije Audit servisa.
- Uskladiti pozive IAccessDecision sa API vlasnikom.
- Izvrsiti zavrsne provjere i testove.
