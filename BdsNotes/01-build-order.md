# Build Order

## What we are NOT doing

Top-down by layer:
1. ~~Import BDDs~~
2. ~~Derive overall architecture from the full BDD set~~
3. ~~Build foundation (data types) + unit tests~~
4. ~~Build DAL and I/O + unit tests~~
5. ~~Then behaviors~~

This is the order that makes sense in OOP. It is backwards for F# for two
reasons:

1. **Types and behaviors aren't separate layers in F#.** A discriminated
   union *is* the design of every workflow that touches it. Designing types
   in isolation produces anemic record bags with the same pathology as
   anemic C# entities — domain logic floating in service classes.
2. **DAL-before-behaviors shapes the domain around the database.** The
   FP-canonical move is the opposite: design pure in-memory workflows
   first, push persistence to the edges. "Functional core, imperative
   shell." Building DAL first gets you Active Record poisoning in F#
   syntax.

## What we ARE doing: vertical slices

1. Import the BDDs. Triage out any that have become implementation-shaped.
2. Resist BDUF. F#'s refactor story (compiler-enforced exhaustive matching,
   type-driven change propagation) is good enough that we can afford to
   discover architecture rather than plan it.
3. Pick **one** vertical slice. End to end. Probably the most central
   behavior in the domain — for LeoBloom that's likely transaction
   import + reconciliation.
4. For that slice:
   - Design domain types **from** the BDD scenarios.
   - Write the workflow as a pure function. No DB. No IO.
   - Unit tests come directly off the BDDs.
5. Wire IO/DAL at the edges to feed the pure core.
6. Integration test the seam.
7. Repeat for the next slice.

## Why this is uncomfortable

It feels under-architected on day one. There will be no "foundation" laid
before the first behavior ships. That's intentional. After 3-5 slices,
patterns emerge — patterns grounded in concrete cases, not patterns invented
in a vacuum. Then we formalize the architecture. Not before.

Better to be ignorant cheaply than to be wrong expensively.

## Note on cross-slice reuse

When slice #3 looks similar to slice #1, resist the urge to extract a
shared abstraction. Wait for slice #5. The third occurrence is when the
real pattern reveals itself; the second occurrence is a coincidence.
"Three strikes and you refactor" — applies double in FP because premature
abstraction in a type-driven language fossilizes faster than in OOP.
