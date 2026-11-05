
import type {
  Course,
  CreateCourseRequest,
} from '../types/course'

const DEV_USER_ID = '00000000-0000-0000-0000-000000000001'

// Samo za Development okruženje.
function getHeaders(): Record<string, string> {
  return import.meta.env.DEV
    ? { 'X-Test-User': DEV_USER_ID }
    : {}
}

async function checkResponse(response: Response): Promise<void> {
  if (response.ok) return

  const payload = await response.json().catch(() => null)

  const message =
    payload?.detail ??
    payload?.title ??
    `HTTP greška ${response.status}`

  throw new Error(message)
}

export const courseService = {
  async createCourse(
    input: CreateCourseRequest
  ): Promise<Course> {
    const response = await fetch('/api/courses', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...getHeaders(),
      },
      body: JSON.stringify(input),
    })

    await checkResponse(response)

    return (await response.json()) as Course
  },

  async archiveCourse(courseId: string): Promise<void> {
    const response = await fetch(
      `/api/courses/${encodeURIComponent(courseId)}/archive`,
      {
        method: 'POST',
        headers: getHeaders(),
      }
    )

    await checkResponse(response)

    // Uspešan odgovor je 204 No Content.
    // Ne pokušavamo da čitamo JSON.
  },
}
