import { useState } from 'react'
import {
  addMember,
  createGroup,
  grantMaterialAccess,
  listGroups,
  listMembers,
  listVisibleMaterials,
  removeMember,
} from '../../api/acc06'
import type { Group, GroupMember, Material } from '../../api/acc06'

type Props = {
  userId: string
  courseId: string
  archived: boolean
  students: readonly { id: string; name: string }[]
}

export default function GroupManagement({
  userId,
  courseId,
  archived,
  students,
}: Props) {
  const [groups, setGroups] = useState<Group[] | null>(null)
  const [groupId, setGroupId] = useState('')
  const [members, setMembers] = useState<GroupMember[] | null>(null)
  const [groupName, setGroupName] = useState('')
  const [studentId, setStudentId] = useState('')
  const [materials, setMaterials] = useState<Material[] | null>(null)
  const [materialKey, setMaterialKey] = useState('')
  const [busy, setBusy] = useState(false)
  const [status, setStatus] = useState<{
    text: string
    error: boolean
  } | null>(null)

  const selectedGroup = groups?.find(group => group.id === groupId)
  const canEditGroup = !archived && selectedGroup?.status === 'Active'

  const selectedMaterial = materials?.find(
    material => `${material.type}:${material.materialId}` === materialKey,
  )

  const availableStudents = students.filter(
    student => !members?.some(member => member.userId === student.id),
  )

  async function run(action: () => Promise<void>, success: string) {
    setBusy(true)
    setStatus(null)

    try {
      await action()
      setStatus({ text: success, error: false })
    } catch (error) {
      setStatus({
        text: error instanceof Error ? error.message : 'Zahtev nije uspeo.',
        error: true,
      })
    } finally {
      setBusy(false)
    }
  }

  async function loadGroups() {
    setGroups(null)
    setGroupId('')
    setMembers(null)
    setStudentId('')
    setMaterialKey('')

    setGroups(await listGroups(userId, courseId))
  }

  async function selectGroup(id: string) {
    setGroupId(id)
    setMembers(null)
    setStudentId('')
    setMaterialKey('')
    setStatus(null)

    if (id) {
      await run(async () => {
        setMembers(await listMembers(userId, courseId, id))
      }, 'Članovi su učitani.')
    }
  }

  async function saveGroup() {
    if (groups === null || !groupName.trim()) return

    const created = await createGroup(userId, courseId, groupName.trim())

    setGroups([...groups, created])
    setGroupName('')
    setGroupId(created.id)
    setMembers([])
    setStudentId('')
    setMaterialKey('')
  }

  async function saveMember() {
    if (!canEditGroup || members === null || !studentId) return

    const created = await addMember(userId, courseId, groupId, studentId)

    setMembers([...members, created])
    setStudentId('')
  }

  async function deleteMember(id: string) {
    await removeMember(userId, courseId, groupId, id)

    setMembers(current =>
      current?.filter(member => member.userId !== id) ?? null,
    )
  }

  async function loadMaterials() {
    setMaterialKey('')
    setMaterials(null)

    setMaterials(await listVisibleMaterials(userId, courseId))
  }

  async function saveMaterialGrant() {
    if (!canEditGroup || !selectedGroup || !selectedMaterial) return

    const group = selectedGroup
    const material = selectedMaterial

    await run(async () => {
      await grantMaterialAccess(userId, courseId, group.id, material)
      setMaterialKey('')
    }, `Materijal ${material.materialId} je dodeljen grupi „${group.name}“.`)
  }

  return (
    <>
      <fieldset className="work-panel" disabled={busy}>
        <legend>Grupe i članovi</legend>

        <button
          type="button"
          onClick={() => void run(loadGroups, 'Grupe su učitane.')}
        >
          Učitaj grupe
        </button>

        {groups?.length === 0 && (
          <p className="hint">Kurs nema grupa.</p>
        )}

        {!archived && groups !== null && (
          <form onSubmit={event => {
            event.preventDefault()
            if (!groupName.trim()) return
            void run(saveGroup, 'Grupa je kreirana.')
          }}>
            <label>
              Naziv nove grupe
              <input
                required
                maxLength={100}
                value={groupName}
                onChange={event => setGroupName(event.target.value)}
              />
            </label>

            <button type="submit" disabled={!groupName.trim()}>
              Kreiraj grupu
            </button>
          </form>
        )}

        {groups && groups.length > 0 && (
          <label>
            Grupa
            <select
              value={groupId}
              onChange={event => void selectGroup(event.target.value)}
            >
              <option value="">Izaberi grupu</option>

              {groups.map(group => (
                <option key={group.id} value={group.id}>
                  {group.name} · {group.status === 'Archived' ? 'arhivirana' : 'aktivna'}
                </option>
              ))}
            </select>
          </label>
        )}

        {selectedGroup && members === null && !busy && (
          <button
            type="button"
            className="secondary"
            onClick={() => void selectGroup(groupId)}
          >
            Ponovo učitaj članove
          </button>
        )}

        {members?.length === 0 && (
          <p className="hint">Grupa nema članova.</p>
        )}

        {members && members.length > 0 && (
          <ul>
            {members.map(member => {
              const name = students.find(
                student => student.id === member.userId,
              )?.name ?? member.userId

              return (
                <li key={member.id}>
                  <div className="action-row">
                    <span className="hint">{name}</span>

                    {canEditGroup && (
                      <button
                        type="button"
                        className="secondary"
                        aria-label={`Ukloni člana ${name}`}
                        onClick={() => void run(
                          () => deleteMember(member.userId),
                          'Član je uklonjen.',
                        )}
                      >
                        Ukloni
                      </button>
                    )}
                  </div>
                </li>
              )
            })}
          </ul>
        )}

        {canEditGroup && members !== null && (
          <form onSubmit={event => {
            event.preventDefault()
            if (!studentId) return
            void run(saveMember, 'Član je dodat.')
          }}>
            <label>
              Dodaj demo studenta
              <select
                value={studentId}
                onChange={event => setStudentId(event.target.value)}
              >
                <option value="">Izaberi studenta</option>

                {availableStudents.map(student => (
                  <option key={student.id} value={student.id}>
                    {student.name}
                  </option>
                ))}
              </select>
            </label>

            <button type="submit" disabled={!studentId}>
              Dodaj člana
            </button>
          </form>
        )}
      </fieldset>

      {!archived && (
        <fieldset className="work-panel" disabled={busy}>
          <legend>Vidljivost materijala po grupi</legend>

          {canEditGroup && selectedGroup ? (
            <>
              <p className="hint">
                Izabrana grupa: <strong>{selectedGroup.name}</strong>
              </p>

              <button
                type="button"
                className="secondary"
                onClick={() => void run(loadMaterials, 'Materijali su učitani.')}
              >
                Učitaj materijale kursa
              </button>

              {materials?.length === 0 && (
                <p className="hint">Kurs nema materijala za dodelu.</p>
              )}

              {materials && materials.length > 0 && (
                <form onSubmit={event => {
                  event.preventDefault()
                  void saveMaterialGrant()
                }}>
                  <label>
                    Materijal
                    <select
                      value={materialKey}
                      onChange={event => setMaterialKey(event.target.value)}
                    >
                      <option value="">Izaberi materijal</option>

                      {materials.map(material => {
                        const key = `${material.type}:${material.materialId}`

                        return (
                          <option key={key} value={key}>
                            {material.type} · {material.materialId}
                          </option>
                        )
                      })}
                    </select>
                  </label>

                  <p className="hint">
                    Ako je materijal dostupan celom kursu, posle prve dodele
                    mogu da ga vide samo studenti iz izabrane grupe. Ako je
                    već ograničen na grupe, postojeće dodele ostaju na snazi.
                  </p>

                  <button type="submit" disabled={!selectedMaterial}>
                    Dodeli grupi
                  </button>
                </form>
              )}
            </>
          ) : (
            <p className="hint">Izaberi aktivnu grupu u odeljku iznad.</p>
          )}
        </fieldset>
      )}

      <div
        className={`result${status?.error ? ' error' : ''}`}
        role="status"
        aria-live="polite"
      >
        {busy ? 'Zahtev je u toku…' : status?.text}
      </div>
    </>
  )
}