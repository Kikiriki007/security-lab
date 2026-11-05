
import { useEffect, useState } from 'react'
import CourseworkKt1 from './components/CourseworkKt1'
import CreateAccountForm from './components/CreateAccountForm'
import CoursesPage from './pages/CoursesPage'

type Page = 'home' | 'courses'

export default function App() {
  const [health, setHealth] = useState('Checking API…')
  const [currentPage, setCurrentPage] = useState<Page>('home')

  useEffect(() => {
    fetch('/api/health')
      .then((response) =>
        response.ok
          ? response.json()
          : Promise.reject(response.status)
      )
      .then((data) =>
        setHealth(
          data.status === 'ok'
            ? 'API available'
            : 'API is not ready'
        )
      )
      .catch(() =>
        setHealth('API unavailable (start the backend).')
      )
  }, [])

  return (
    <main>
      <header className="app-header">
        <p className="eyebrow">Security Lab / OIB</p>
        <h1>Security Lab</h1>

        <p>
          An interactive laboratory platform for the
          Fundamentals of Information Security course.
          Develop features through the assigned project components.
        </p>

        <nav
          className="app-navigation"
          aria-label="Glavna navigacija"
        >
          <button
            type="button"
            className={`nav-button ${
              currentPage === 'home' ? 'nav-active' : ''
            }`}
            onClick={() => setCurrentPage('home')}
          >
            Početna
          </button>

          <button
            type="button"
            className={`nav-button ${
              currentPage === 'courses' ? 'nav-active' : ''
            }`}
            onClick={() => setCurrentPage('courses')}
          >
            Kursevi (ACC-01)
          </button>
        </nav>
      </header>

      {currentPage === 'home' ? (
        <>
          <section>
            <h2>Development status</h2>
            <p>{health}</p>
            <p>
              KT1: initial implementation, component architecture,
              and data flow diagram.
            </p>
          </section>

          <section>
            <h2>Kreiranje naloga (administrator)</h2>
            <CreateAccountForm />
          </section>

          <CourseworkKt1 />
        </>
      ) : (
        <CoursesPage />
      )}
    </main>
  )
}
